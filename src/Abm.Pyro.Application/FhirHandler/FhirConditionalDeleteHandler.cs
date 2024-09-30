using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.SearchQuery;
using Abm.Pyro.Application.Validation;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirConditionalDeleteHandler(
    IValidator validator,
    IFhirResourceTypeSupport fhirResourceTypeSupport,
    ISearchQueryService searchQueryService,
    IResourceStoreSearch resourceStoreSearch,
    IRequestHandler<FhirDeleteRequest, FhirOptionalResourceResponse> fhirDeleteHandler,
    IOperationOutcomeSupport operationOutcomeSupport)
    : IRequestHandler<FhirConditionalDeleteRequest, FhirOptionalResourceResponse>, IFhirConditionalDeleteHandler
{
    public async Task<FhirOptionalResourceResponse> Handle(string tenant, string resourceName, string query, Dictionary<string, StringValues> headers, CancellationToken cancellationToken)
    {
        return await Handle(new FhirConditionalDeleteRequest(
            RequestSchema: "https",
            tenant: tenant,
            RequestPath: string.Empty,
            QueryString: query,
            Headers: headers,
            ResourceName: resourceName,
            TimeStamp: DateTimeOffset.Now),
            cancellationToken: cancellationToken);
    }
    
    public async Task<FhirOptionalResourceResponse> Handle(FhirConditionalDeleteRequest request,
        CancellationToken cancellationToken)
    {
        
        ValidatorResult requestValidatorResult = validator.Validate(request);
        if (!requestValidatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(requestValidatorResult);
        }
        
        FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(request.ResourceName);
        
        SearchQueryServiceOutcome searchQueryServiceOutcome = await searchQueryService.Process(fhirResourceType, request.QueryString);
        ValidatorResult searchQueryValidatorResult = validator.Validate(new SearchQueryServiceOutcomeAndHeaders(
            searchQueryServiceOutcome: searchQueryServiceOutcome, 
            headers: request.Headers));
        if (!searchQueryValidatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(searchQueryValidatorResult);
        }

        ResourceStoreSearchOutcome resourceStoreSearchOutcome = await resourceStoreSearch.GetSearch(searchQueryServiceOutcome);
        
        //See FHIR Specification: 3.1.0.7.1 Conditional delete
        //https://hl7.org/fhir/R4/http.html#3.1.0.7.1
        
        if (IsMoreThanOneResourceMatch(resourceStoreSearchOutcome.SearchTotal))
        {
            //Multiple matches: A server may choose to delete all the matching resources, or it may choose to return a 412 Precondition Failed error indicating the client's
            //criteria were not selective enough. A server indicates whether it can delete multiple resources in its Capability Statement (.rest.resource.conditionalDelete)
            return PreconditionFailed();
        }

        if (NoResourceMatch(resourceStoreSearchOutcome.SearchTotal))
        {
            //No matches : The server performs an ordinary delete action and returns 204 NoContent
            return NoContentResourceNotFound();
        }

        if (IsSingleResourceMatch(resourceStoreSearchOutcome.SearchTotal))
        {
            //No matches: The server performs an ordinary delete on the matching resource
            return await PerformNormalDelete(request, resourceStoreSearchOutcome.ResourceStoreList.First().ResourceId, cancellationToken);
        }
        
        throw new ApplicationException($"Conditional update has encountered and unknown action.");

    }

    private static FhirOptionalResourceResponse InvalidValidatorResultResponse(ValidatorResult validatorResult)
    {
        return new FhirOptionalResourceResponse(
            Resource: validatorResult.GetOperationOutcome(), 
            HttpStatusCode: validatorResult.GetHttpStatusCode(),
            Headers: new Dictionary<string, StringValues>());
    }
    
    private static FhirOptionalResourceResponse InValidSearchQueryResponse(FhirResourceResponse? searchQueryValidationResponse)
    {
        if (searchQueryValidationResponse is null)
        {
            throw new NullReferenceException(nameof(searchQueryValidationResponse));
        }

        return new FhirOptionalResourceResponse(Resource: searchQueryValidationResponse.Resource, HttpStatusCode: searchQueryValidationResponse.HttpStatusCode,
            Headers: searchQueryValidationResponse.Headers);
    }

    private FhirOptionalResourceResponse PreconditionFailed()
    {
        return new FhirOptionalResourceResponse(
            Resource: operationOutcomeSupport.GetError(
                new[]
                {
                    $"Conditional delete criteria was not selective enough, more than a single resource was found to match. This server only supports the conditional delete of type: Single."
                }),
            HttpStatusCode: HttpStatusCode.PreconditionFailed,
            Headers: new Dictionary<string, StringValues>());
    }

    private FhirOptionalResourceResponse NoContentResourceNotFound()
    { 
        return new FhirOptionalResourceResponse(
            Resource: operationOutcomeSupport.GetError(
                new[]
                {
                    $"Conditional update criteria returned a single matched resource, however its resource id did not match the request's resource's id."
                }),
            HttpStatusCode: HttpStatusCode.NoContent,
            Headers: new Dictionary<string, StringValues>());
    }

    private async Task<FhirOptionalResourceResponse> PerformNormalDelete(FhirConditionalDeleteRequest request, string resourceId, CancellationToken cancellationToken)
    {
        FhirResponse.FhirResponse fhirResponse = await fhirDeleteHandler.Handle(
            new FhirDeleteRequest(
                RequestSchema: request.RequestSchema,
                tenant: request.tenant,
                RequestPath: request.RequestPath,
                QueryString: request.QueryString,
                Headers: request.Headers, 
                ResourceName: request.ResourceName, 
                ResourceId: resourceId, 
                TimeStamp: request.TimeStamp), 
            cancellationToken);

        return new FhirOptionalResourceResponse(Resource: null, fhirResponse.HttpStatusCode, Headers: fhirResponse.Headers);
    }

    public static bool NoResourceMatch(int searchTotal)
    {
        return (searchTotal == 0);
    }

    public static bool IsSingleResourceMatch(int searchTotal)
    {
        return (searchTotal == 1);
    }

    public static bool IsMoreThanOneResourceMatch(int searchTotal)
    {
        return (searchTotal > 1);
    }

}
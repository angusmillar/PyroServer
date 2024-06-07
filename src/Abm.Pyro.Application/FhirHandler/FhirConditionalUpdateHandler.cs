using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.SearchQuery;
using Abm.Pyro.Application.Validation;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirConditionalUpdateHandler(
    IValidator validator,
    IFhirResourceTypeSupport fhirResourceTypeSupport,
    ISearchQueryService searchQueryService,
    IResourceStoreSearch resourceStoreSearch,
    IRequestHandler<FhirUpdateRequest, FhirOptionalResourceResponse> fhirUpdateHandler,
    IRequestHandler<FhirCreateRequest, FhirOptionalResourceResponse> fhirCreateHandler,
    IOperationOutcomeSupport operationOutcomeSupport)
    : IRequestHandler<FhirConditionalUpdateRequest, FhirOptionalResourceResponse>, IFhirConditionalUpdateHandler
{
    public async Task<FhirOptionalResourceResponse> Handle(string query, Resource resource, Dictionary<string, StringValues> headers, CancellationToken cancellationToken)
    {
        return await Handle(new FhirConditionalUpdateRequest(
                RequestSchema: "https",
                RequestPath: string.Empty,
                QueryString: query,
                Headers: headers,
                ResourceName: resource.TypeName,
                Resource: resource,
                TimeStamp: DateTimeOffset.Now),
            cancellationToken: cancellationToken);
    }
    
    public async Task<FhirOptionalResourceResponse> Handle(FhirConditionalUpdateRequest request,
        CancellationToken cancellationToken)
    {
        ValidatorResult requestValidatorResult = validator.Validate(request);
        if (!requestValidatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(requestValidatorResult);
        }
        
        FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(request.Resource.TypeName);
        
        SearchQueryServiceOutcome searchQueryServiceOutcome = await searchQueryService.Process(fhirResourceType, request.QueryString);
        ValidatorResult searchQueryValidatorResult = validator.Validate(new SearchQueryServiceOutcomeAndHeaders(
            searchQueryServiceOutcome: searchQueryServiceOutcome, 
            headers: request.Headers));
        if (!searchQueryValidatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(searchQueryValidatorResult);
        }

        ResourceStoreSearchOutcome resourceStoreSearchOutcome = await resourceStoreSearch.GetSearch(searchQueryServiceOutcome);
        
        //See FHIR Specification: 3.1.0.4.3 Conditional update
        //https://hl7.org/fhir/R4/http.html#cond-update
        
        if (IsMoreThanOneResourceMatch(resourceStoreSearchOutcome.SearchTotal))
        {
            //Multiple matches: The server returns a 412 Precondition Failed error indicating the client's criteria were not selective enough preferably with an OperationOutcome
            return PreconditionFailed();
        }

        if (IsSingleResourceMatch(resourceStoreSearchOutcome.SearchTotal) && ResourceIdProvided(request.Resource.Id) &&
            !MatchedResourceIdEqualsProvidedResourcedId(request.Resource.Id, resourceStoreSearchOutcome.ResourceStoreList.First().ResourceId))
        {
            //One Match, resource id provided but does not match resource found: The server returns a 400 Bad Request error indicating the client id
            //specification was a problem preferably with an OperationOutcome
            return BadRequestResourceIdsMismatch();
        }

        if (IsSingleResourceMatch(resourceStoreSearchOutcome.SearchTotal) && (!ResourceIdProvided(request.Resource.Id) ||
                                                                              MatchedResourceIdEqualsProvidedResourcedId(request.Resource.Id,
                                                                                  resourceStoreSearchOutcome.ResourceStoreList.First().ResourceId)))
        {
            if (!ResourceIdProvided(request.Resource.Id))
            {
                request.Resource.Id = resourceStoreSearchOutcome.ResourceStoreList.First().ResourceId;
            }
            
            //One Match, no resource id provided OR (resource id provided and it matches the found resource): The server performs the update against the matching resource
            return await fhirUpdateHandler.Handle(new FhirUpdateRequest(
                    RequestSchema: request.RequestSchema,
                    RequestPath: request.RequestPath,
                    QueryString: request.QueryString,
                    Headers: request.Headers,
                    ResourceName: request.ResourceName,
                    Resource: request.Resource,
                    ResourceId: resourceStoreSearchOutcome.ResourceStoreList.First().ResourceId, 
                    TimeStamp: request.TimeStamp),
                cancellationToken);
        }

        if (NoResourceMatch(resourceStoreSearchOutcome.SearchTotal) && !ResourceIdProvided(request.Resource.Id))
        {
            //No matches, no id provided: The server creates the resource.
            return await fhirCreateHandler.Handle(new FhirCreateRequest(
                RequestSchema: request.RequestSchema,
                RequestPath: request.RequestPath,
                QueryString: request.QueryString,
                Headers: request.Headers,
                ResourceName: request.ResourceName,
                Resource: request.Resource,
                ResourceId: null, 
                TimeStamp: request.TimeStamp
            ), cancellationToken);
        }

        if (NoResourceMatch(resourceStoreSearchOutcome.SearchTotal) && ResourceIdProvided(request.Resource.Id))
        {
            //No matches, id provided: The server treats the interaction as an Update as Create interaction (or rejects it, if it does not support Update as Create)
            
            return await fhirUpdateHandler.Handle(new FhirUpdateRequest(
                    RequestSchema: request.RequestSchema,
                    RequestPath: request.RequestPath,
                    QueryString: request.QueryString,
                    Headers: request.Headers,
                    ResourceName: request.ResourceName,
                    Resource: request.Resource,
                    ResourceId: request.Resource.Id, 
                    TimeStamp: request.TimeStamp),
                cancellationToken);
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
    
    private FhirOptionalResourceResponse BadRequestResourceIdsMismatch()
    {
        return new FhirOptionalResourceResponse(
            Resource: operationOutcomeSupport.GetError(
                new[]
                {
                    $"Conditional update criteria returned a single matched resource, however its resource id did not match the request's resource's id."
                }),
            HttpStatusCode: HttpStatusCode.BadRequest,
            Headers: new Dictionary<string, StringValues>());
    }

    private FhirOptionalResourceResponse PreconditionFailed()
    {
        return new FhirOptionalResourceResponse(
            Resource: operationOutcomeSupport.GetError(
                new[]
                {
                    $"Conditional update criteria was not selective enough, more than a single resource was found to match."
                }),
            HttpStatusCode: HttpStatusCode.PreconditionFailed,
            Headers: new Dictionary<string, StringValues>());
    }

    public static bool MatchedResourceIdEqualsProvidedResourcedId(string matchedResourceId,
        string providedResourceId)
    {
        return (matchedResourceId.Equals(providedResourceId, StringComparison.Ordinal));
    }

    public static bool ResourceIdProvided(string? resourceId)
    {
        return (!string.IsNullOrWhiteSpace(resourceId));
    }

    public static bool NoResourceMatch(int searchTotal)
    {
        return (searchTotal == 0);
    }

    public static  bool IsSingleResourceMatch(int searchTotal)
    {
        return (searchTotal == 1);
    }

    public static  bool IsMoreThanOneResourceMatch(int searchTotal)
    {
        return (searchTotal > 1);
    }

}
using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.Notification;
using Abm.Pyro.Application.SearchQuery;
using Abm.Pyro.Application.Validation;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirConditionalCreateHandler(
        IValidator validator,
    IFhirResourceTypeSupport fhirResourceTypeSupport,
    IFhirRequestHttpHeaderSupport fhirRequestHttpHeaderSupport,
    IRequestHandler<FhirCreateRequest, FhirOptionalResourceResponse> fhirCreateHandler,
    ISearchQueryService searchQueryService,
    IResourceStoreSearch resourceStoreSearch,
     IRepositoryEventCollector repositoryEventCollector)
    : IRequestHandler<FhirConditionalCreateRequest, FhirOptionalResourceResponse>
{
    public async Task<FhirOptionalResourceResponse> Handle(FhirConditionalCreateRequest request,
        CancellationToken cancellationToken)
    {
        ValidatorResult requestValidatorResult = validator.Validate(request);
        if (!requestValidatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(requestValidatorResult);
        }
        
        FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(request.ResourceName);
        
        string? ifNoneExistQueryString = fhirRequestHttpHeaderSupport.GetIfNoneExist(request.Headers);
        if (ifNoneExistQueryString is null)
        {
            //If the If-None-Exist header is no found then treat it as a normal Create 
            return await ProcessAsCreate(request, cancellationToken);
        }

        SearchQueryServiceOutcome searchQueryServiceOutcome = await searchQueryService.Process(fhirResourceType, ifNoneExistQueryString);
        ValidatorResult searchQueryValidatorResult = validator.Validate(new SearchQueryServiceOutcomeAndHeaders(
            searchQueryServiceOutcome: searchQueryServiceOutcome, 
            headers: request.Headers));
        if (!searchQueryValidatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(searchQueryValidatorResult);
        }
        
        ResourceStoreSearchOutcome resourceStoreSearchOutcome = await resourceStoreSearch.GetSearch(searchQueryServiceOutcome);
        if (NoMatches(resourceStoreSearchOutcome))
        {
            //No matches: The server processes the create as a normal Create 
            return await ProcessAsCreate(request, cancellationToken);
        }

        if (OneMatch(resourceStoreSearchOutcome))
        {
            //One Match: The server ignores the post and returns 200 OK
            return OkResponse(resourceStoreSearchOutcome.ResourceStoreList.First());
        }

        if (MultipleMatches(resourceStoreSearchOutcome))
        {
            //Multiple matches: The server returns a 412 Precondition Failed error indicating the client's criteria were not selective enough
            return PreconditionFailedResponse();
        }

        throw new ApplicationException($"Conditional create has encountered and unknown action.");
    }
    
    private FhirOptionalResourceResponse InvalidValidatorResultResponse(ValidatorResult validatorResult)
    {
        return new FhirOptionalResourceResponse(
            Resource: validatorResult.GetOperationOutcome(), 
            HttpStatusCode: validatorResult.GetHttpStatusCode(),
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }
    
    private FhirOptionalResourceResponse InvalidSearchQueryResponse(FhirResourceResponse? searchQueryValidationResponse)
    {
        if (searchQueryValidationResponse is null)
        {
            throw new NullReferenceException(nameof(searchQueryValidationResponse));
        }

        repositoryEventCollector.Clear();
        return new FhirOptionalResourceResponse(
            Resource: searchQueryValidationResponse.Resource,
            HttpStatusCode: searchQueryValidationResponse.HttpStatusCode,
            Headers: searchQueryValidationResponse.Headers,
            RepositoryEventCollector: repositoryEventCollector);
    }

    private async Task<FhirOptionalResourceResponse> ProcessAsCreate(FhirConditionalCreateRequest request,
        CancellationToken cancellationToken)
    {
        return await fhirCreateHandler.Handle(new FhirCreateRequest(
                RequestSchema: request.RequestSchema,
                Tenant: request.Tenant,
                RequestId: request.RequestId,
                RequestPath: request.RequestPath,
                QueryString: request.QueryString,
                Headers: new Dictionary<string, StringValues>(),
                ResourceName: request.ResourceName,
                Resource: request.Resource,
                ResourceId: null,
                TimeStamp: request.TimeStamp),
            cancellationToken);
    }

    private FhirOptionalResourceResponse PreconditionFailedResponse()
    {
        repositoryEventCollector.Clear();
        return new FhirOptionalResourceResponse(
            Resource: null,
            HttpStatusCode: HttpStatusCode.PreconditionFailed,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }

    private FhirOptionalResourceResponse OkResponse(ResourceStore resourceStore)
    {
        return new FhirOptionalResourceResponse(
            Resource: null,
            HttpStatusCode: HttpStatusCode.OK,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector,
            ResourceOutcomeInfo: new ResourceOutcomeInfo(
                resourceId: resourceStore.ResourceId, 
                versionId: resourceStore.VersionId));
    }

    public static bool MultipleMatches(ResourceStoreSearchOutcome resourceStoreSearchOutcome)
    {
        return resourceStoreSearchOutcome.SearchTotal > 1;
    }

    public static bool OneMatch(ResourceStoreSearchOutcome resourceStoreSearchOutcome)
    {
        return resourceStoreSearchOutcome.SearchTotal == 1;
    }

    public static bool NoMatches(ResourceStoreSearchOutcome resourceStoreSearchOutcome)
    {
        return resourceStoreSearchOutcome.SearchTotal == 0;
    }
}
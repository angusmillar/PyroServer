using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.Notification;
using Abm.Pyro.Application.SearchQuery;
using Abm.Pyro.Application.Validation;
using Hl7.Fhir.Model;
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

public class FhirHistoryTypeLevelHandler(
    IValidator validator,
    IFhirResourceTypeSupport fhirResourceTypeSupport,
    ISearchQueryService searchQueryService,
    IResourceStoreGetHistoryByResourceType resourceStoreGetHistoryByResourceType,
    IFhirBundleCreationSupport fhirBundleCreationSupport,
    IPaginationSupport paginationSupport,
    IRepositoryEventCollector repositoryEventCollector)
    : IRequestHandler<FhirHistoryTypeLevelRequest, FhirResourceResponse>
{
    public async Task<FhirResourceResponse> Handle(FhirHistoryTypeLevelRequest request,
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
            SearchQueryServiceOutcome: searchQueryServiceOutcome, 
            Headers: request.Headers));
        if (!searchQueryValidatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(searchQueryValidatorResult);
        }

        ResourceStoreSearchOutcome resourceStoreSearchOutcome = await resourceStoreGetHistoryByResourceType.Get(fhirResourceType, searchQueryServiceOutcome);

        AddRepositoryEvents(resourceStoreSearchOutcome, request.RequestId);
        
        Bundle bundle = await fhirBundleCreationSupport.CreateBundle(resourceStoreSearchOutcome, Bundle.BundleType.History, request.RequestSchema);

        await paginationSupport.SetBundlePagination(bundle: bundle,
            searchQueryServiceOutcome: searchQueryServiceOutcome,
            requestSchema: request.RequestSchema,
            requestPath: request.RequestPath,
            pagesTotal: resourceStoreSearchOutcome.PagesTotal,
            pageCurrentlyRequired: resourceStoreSearchOutcome.PageRequested);

        return new FhirResourceResponse(
            Resource: bundle,
            HttpStatusCode: HttpStatusCode.OK,
            Headers: new Dictionary<string, StringValues>(), 
            ResourceOutcomeInfo: null,
            RepositoryEventCollector: repositoryEventCollector);
    }
    
    private FhirResourceResponse InvalidValidatorResultResponse(ValidatorResult validatorResult)
    {
        repositoryEventCollector.Clear();
        return new FhirResourceResponse(
            Resource: validatorResult.GetOperationOutcome(), 
            HttpStatusCode: validatorResult.GetHttpStatusCode(),
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }
    
    private void AddRepositoryEvents(ResourceStoreSearchOutcome resourceStoreSearchOutcome, string requestId)
    {
        AddRepositoryEvent(resourceStoreSearchOutcome.ResourceStoreList, requestId);
        AddRepositoryEvent(resourceStoreSearchOutcome.IncludedResourceStoreList, requestId);
    }

    private void AddRepositoryEvent(List<ResourceStore> resourceStoreList, string requestId)
    {
        foreach (var resourceStore in resourceStoreList)
        {
            repositoryEventCollector.Add(
                resourceType: resourceStore.ResourceType,
                requestId: requestId,
                repositoryEventType: RepositoryEventType.Read, 
                resourceId: resourceStore.ResourceId);
        }
    }
}
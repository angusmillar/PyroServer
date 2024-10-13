using System.Net;
using Abm.Pyro.Application.Cache;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.SearchQuery;
using Abm.Pyro.Application.Validation;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Application.MetaDataService;
using Abm.Pyro.Application.Notification;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirMetaDataHandler(
    IValidator validator,
    ISearchQueryService searchQueryService,
    IMetaDataCache metaDataCache,
    IRepositoryEventCollector repositoryEventCollector)
    : IRequestHandler<FhirMetaDataRequest, FhirResourceResponse>
{
    public async Task<FhirResourceResponse> Handle(FhirMetaDataRequest request,
        CancellationToken cancellationToken)
    {
        ValidatorResult requestValidatorResult = validator.Validate(request);
        if (!requestValidatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(requestValidatorResult);
        }
        
        SearchQueryServiceOutcome searchQueryServiceOutcome = await searchQueryService.Process(FhirResourceTypeId.Resource, request.QueryString);
        ValidatorResult searchQueryValidatorResult = validator.Validate(new SearchQueryServiceOutcomeAndHeaders(
            SearchQueryServiceOutcome: searchQueryServiceOutcome, 
            Headers: request.Headers));
        if (!searchQueryValidatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(searchQueryValidatorResult);
        }

        CapabilityStatement capabilityStatement = await metaDataCache.GetCapabilityStatement();
        
        return new FhirResourceResponse(
            Resource: capabilityStatement,
            HttpStatusCode: HttpStatusCode.OK,
            Headers: new Dictionary<string, StringValues>(), 
            ResourceOutcomeInfo: null,
            RepositoryEventCollector: repositoryEventCollector);
    }
    
    private FhirResourceResponse InvalidValidatorResultResponse(ValidatorResult validatorResult)
    {
        return new FhirResourceResponse(
            Resource: validatorResult.GetOperationOutcome(), 
            HttpStatusCode: validatorResult.GetHttpStatusCode(),
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }
}
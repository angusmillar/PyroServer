using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.Indexing;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Application.Validation;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.Validation;
using SummaryType = Hl7.Fhir.Rest.SummaryType;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirCreateHandler(
    IValidator validator,
    IResourceStoreAdd resourceStoreAdd,
    IFhirSerializationSupport fhirSerializationSupport,
    IFhirResourceTypeSupport fhirResourceTypeSupport,
    IFhirResponseHttpHeaderSupport fhirResponseHttpHeaderSupport,
    IIndexer indexer,
    IPreferredReturnTypeService preferredReturnTypeService,
    IServiceBaseUrlCache serviceBaseUrlCache)
    : IRequestHandler<FhirCreateRequest, FhirOptionalResourceResponse>, IFhirCreateHandler
{
    public Task<FhirOptionalResourceResponse> Handle(string tenant, string resourceId, Resource resource, Dictionary<string, StringValues> headers, CancellationToken cancellationToken)
    {
        return Handle(new FhirCreateRequest(
            RequestSchema: "http",
            tenant: tenant,
            RequestPath: string.Empty,
            QueryString: null,
            Headers: headers,
            ResourceName: resource.TypeName,
            Resource: resource,
            ResourceId: resourceId,
            TimeStamp: DateTimeOffset.Now), cancellationToken: cancellationToken);
    }
    
    public async Task<FhirOptionalResourceResponse> Handle(FhirCreateRequest request,
        CancellationToken cancellationToken)
    {

        ValidatorResult validatorResult = validator.Validate(request);
        if (!validatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(validatorResult);
        }
        
        if (string.IsNullOrWhiteSpace(request.ResourceId))
        {
            request.Resource.Id = GuidSupport.NewFhirGuid();
        }

        FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(request.Resource.TypeName);

        SetResourceMeta(request.Resource, request.TimeStamp);

        IndexerOutcome indexerOutcome = await indexer.Process(request.Resource, fhirResourceType);

        var resourceStore = new ResourceStore(
            resourceStoreId: null,
            resourceId: request.Resource.Id,
            versionId: 1,
            isCurrent: true,
            isDeleted: false,
            resourceType: fhirResourceType,
            httpVerb: request.HttpVerbId,
            json: fhirSerializationSupport.ToJson(request.Resource, SummaryType.False, pretty: false),
            lastUpdatedUtc: request.Resource.Meta!.LastUpdated!.Value.UtcDateTime,
            indexReferenceList: indexerOutcome.ReferenceIndexList,
            indexStringList: indexerOutcome.StringIndexList,
            indexDateTimeList: indexerOutcome.DateTimeIndexList,
            indexQuantityList: indexerOutcome.QuantityIndexList,
            indexTokenList: indexerOutcome.TokenIndexList,
            indexUriList: indexerOutcome.UriIndexList,
            rowVersion: 0
        );
  
        resourceStore = await resourceStoreAdd.Add(resourceStore);
        ServiceBaseUrl serviceBaseUrl = await serviceBaseUrlCache.GetRequiredPrimaryAsync();
        
        var responseHeaders = fhirResponseHttpHeaderSupport.ForCreate(
            resourceType: resourceStore.ResourceType,
            lastUpdatedUtc: resourceStore.LastUpdatedUtc,
            resourceId: resourceStore.ResourceId,
            versionId: resourceStore.VersionId,
            requestTimeStamp: request.TimeStamp,
            requestSchema: request.RequestSchema,
            serviceBaseUrl: serviceBaseUrl.Url);

        return preferredReturnTypeService.GetResponse(HttpStatusCode.Created, request.Resource, resourceStore.VersionId, request.Headers, responseHeaders);
    }

    private static FhirOptionalResourceResponse InvalidValidatorResultResponse(ValidatorResult validatorResult)
    {
        return new FhirOptionalResourceResponse(
            Resource: validatorResult.GetOperationOutcome(), 
            HttpStatusCode: validatorResult.GetHttpStatusCode(),
            Headers: new Dictionary<string, StringValues>());
    }

    private static void SetResourceMeta(Resource resource,
        DateTimeOffset requestTimeStamp)
    {
        resource.Meta ??= new Meta();
        resource.Meta.VersionId = "1";
        resource.Meta.LastUpdated = requestTimeStamp;
    }
}
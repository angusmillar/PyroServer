using System.Collections.Concurrent;
using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.Notification;
using MediatR;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirDeleteHandler(
    IValidator validator,
    IResourceStoreGetForUpdateByResourceId resourceStoreGetForUpdateByResourceId,
    IFhirResponseHttpHeaderSupport fhirResponseHttpHeaderSupport,
    IResourceStoreUpdate resourceStoreUpdate,
    IResourceStoreAdd resourceStoreAdd,
    IFhirResourceTypeSupport fhirResourceTypeSupport,
    IOptions<IndexingSettings> indexingSettingsOptions,
    IRepositoryEventCollector repositoryEventCollector)
    : IRequestHandler<FhirDeleteRequest, FhirOptionalResourceResponse>, IFhirDeleteHandler
{
    private ResourceStoreUpdateProjection? _previousResourceStore;

    public async Task<FhirOptionalResourceResponse> Handle(string tenant, string requestId, string resourceName, string resourceId,
        CancellationToken cancellationToken, ResourceStoreUpdateProjection? previousResourceStore = null)
    {
        _previousResourceStore = previousResourceStore;

        return await Handle(new FhirDeleteRequest(
            RequestSchema: "http",
            Tenant: tenant,
            RequestId: requestId,
            RequestPath: string.Empty,
            QueryString: null,
            Headers: new Dictionary<string, StringValues>(),
            ResourceName: resourceName,
            ResourceId: resourceId,
            TimeStamp: DateTimeOffset.Now), cancellationToken: cancellationToken);
    }

    public async Task<FhirOptionalResourceResponse> Handle(FhirDeleteRequest request,
        CancellationToken cancellationToken)
    {
        ValidatorResult validatorResult = validator.Validate(request);
        if (!validatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(validatorResult);
        }

        FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(request.ResourceName);


        if (_previousResourceStore is null)
        {
            _previousResourceStore =
                await resourceStoreGetForUpdateByResourceId.Get(fhirResourceType, request.ResourceId);
        }

        if (_previousResourceStore is null)
        {
            repositoryEventCollector.Clear();
            return new FhirOptionalResourceResponse(
                Resource :null, 
                HttpStatusCode: HttpStatusCode.NoContent, 
                Headers: new Dictionary<string, StringValues>(), 
                RepositoryEventCollector: repositoryEventCollector, 
                ResourceOutcomeInfo: null);
        }

        if (_previousResourceStore.IsDeleted)
        {
            repositoryEventCollector.Clear();
            return new FhirOptionalResourceResponse(
                Resource: null,
                HttpStatusCode: HttpStatusCode.NoContent,
                Headers: fhirResponseHttpHeaderSupport.ForDelete(
                    requestTimeStamp: request.TimeStamp,
                    versionId: _previousResourceStore.VersionId),
                RepositoryEventCollector: repositoryEventCollector,
                ResourceOutcomeInfo: new ResourceOutcomeInfo(
                    resourceId: request.ResourceId,
                    versionId: _previousResourceStore.VersionId));
        }

        var deletedResourceStore = new ResourceStore(
            resourceStoreId: null,
            resourceId: request.ResourceId,
            versionId: _previousResourceStore.VersionId + 1,
            isCurrent: true,
            isDeleted: true,
            resourceType: fhirResourceType,
            httpVerb: request.HttpVerbId,
            json: string.Empty,
            lastUpdatedUtc: request.TimeStamp.UtcDateTime,
            indexReferenceList: Array.Empty<IndexReference>().ToList(),
            indexStringList: Array.Empty<IndexString>().ToList(),
            indexDateTimeList: Array.Empty<IndexDateTime>().ToList(),
            indexQuantityList: Array.Empty<IndexQuantity>().ToList(),
            indexTokenList: Array.Empty<IndexToken>().ToList(),
            indexUriList: Array.Empty<IndexUri>().ToList(),
            rowVersion: 0
        );

        _previousResourceStore.IsCurrent = false;
        await resourceStoreUpdate.Update(_previousResourceStore,
            indexingSettingsOptions.Value.RemoveHistoricResourceIndexesOnUpdateOrDelete);
        deletedResourceStore = await resourceStoreAdd.Add(deletedResourceStore);

        AddRepositoryDeleteEvent(deletedResourceStore.ResourceStoreId, request.ResourceId);
        
        return new FhirOptionalResourceResponse(
            Resource: null,
            HttpStatusCode: HttpStatusCode.NoContent,
            Headers: fhirResponseHttpHeaderSupport.ForDelete(
                requestTimeStamp: request.TimeStamp,
                versionId: deletedResourceStore.VersionId),
            RepositoryEventCollector: repositoryEventCollector,
            ResourceOutcomeInfo: new ResourceOutcomeInfo(
                resourceId: deletedResourceStore.ResourceId,
                versionId: deletedResourceStore.VersionId));
    }

    private FhirOptionalResourceResponse InvalidValidatorResultResponse(ValidatorResult validatorResult)
    {
        repositoryEventCollector.Clear();
        return new FhirOptionalResourceResponse(
            Resource: validatorResult.GetOperationOutcome(),
            HttpStatusCode: validatorResult.GetHttpStatusCode(),
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector,
            ResourceOutcomeInfo: null);
    }
    
    private void AddRepositoryDeleteEvent(int? resourceStoreId, string requestId)
    {
        if (!resourceStoreId.HasValue)
        {
            throw new ApplicationException(nameof(resourceStoreId));
        }

        repositoryEventCollector.Add(
            requestId: requestId,
            repositoryEventType: RepositoryEventType.Delete, 
            resourceStoreId: resourceStoreId.Value);
    }
}
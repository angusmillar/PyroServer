using System.Net;
using Abm.Pyro.Application.Cache;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.FhirSubscriptions;
using Abm.Pyro.Application.Indexing;
using Abm.Pyro.Application.Notification;
using Hl7.Fhir.Model;
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
using Microsoft.Extensions.Logging;
using SummaryType = Hl7.Fhir.Rest.SummaryType;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirUpdateHandler(
    ILogger<FhirUpdateHandler> logger,
    IValidator validator,
    IFhirResourceTypeSupport fhirResourceTypeSupport,
    IResourceStoreGetForUpdateByResourceId resourceStoreGetForUpdateByResourceId,
    IResourceStoreGetByResourceStoreId resourceStoreGetByResourceStoreId,
    IRequestHandler<FhirCreateRequest, FhirOptionalResourceResponse> fhirCreateHandler,
    IResourceStoreAdd resourceStoreAdd,
    IIndexer indexer,
    IFhirSerializationSupport fhirSerializationSupport,
    IResourceStoreUpdate resourceStoreUpdate,
    IFhirResponseHttpHeaderSupport fhirResponseHttpHeaderSupport,
    IFhirRequestHttpHeaderSupport fhirRequestHttpHeaderSupport,
    IOperationOutcomeSupport operationOutcomeSupport,
    IPreferredReturnTypeService preferredReturnTypeService,
    IOptions<IndexingSettings> indexingSettingsOptions,
    IRepositoryEventCollector repositoryEventCollector,
    IActiveSubscriptionCache activeSubscriptionCache,
    IFhirSubscriptionService fhirSubscriptionService,
    IFhirDeSerializationSupport fhirDeSerializationSupport)
    : IRequestHandler<FhirUpdateRequest, FhirOptionalResourceResponse>, IFhirUpdateHandler
{
    private ResourceStoreUpdateProjection? _previousResourceStore;
    private AcceptSubscriptionOutcome? _acceptSubscriptionOutcome;
    
    public async Task<FhirOptionalResourceResponse> Handle(
        string tenant,
        string requestId,
        string resourceId,
        Resource resource,
        Dictionary<string, StringValues> headers,
        CancellationToken cancellationToken,
        ResourceStoreUpdateProjection? previousResourceStore = null)
    {
        _previousResourceStore = previousResourceStore;

        return await Handle(new FhirUpdateRequest(
            RequestSchema: "http",
            Tenant: tenant,
            RequestId: requestId,
            RequestPath: string.Empty,
            QueryString: null,
            Headers: headers,
            ResourceName: resource.TypeName,
            Resource: resource,
            ResourceId: resourceId,
            TimeStamp: DateTimeOffset.Now), cancellationToken: cancellationToken);
    }

    public async Task<FhirOptionalResourceResponse> HandleSystemSubscriptionUpdate(
        SystemSubscriptionUpdateRequest systemSubscriptionUpdateRequest,
        CancellationToken cancellationToken)
    {
        return await Handle(systemSubscriptionUpdateRequest, cancellationToken: cancellationToken);
    }

    public async Task<FhirOptionalResourceResponse> Handle(
        FhirUpdateRequest request,
        CancellationToken cancellationToken)
    {
        ValidatorResult validatorResult = validator.Validate(request);
        if (!validatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(validatorResult);
        }

        FhirResourceTypeId fhirResourceType =
            fhirResourceTypeSupport.GetRequiredFhirResourceType(request.Resource.TypeName);

        if (_previousResourceStore is null)
        {
            _previousResourceStore =
                await resourceStoreGetForUpdateByResourceId.Get(fhirResourceType, request.Resource.Id);
        }

        if (_previousResourceStore is null)
        {
            return await fhirCreateHandler.Handle(new FhirCreateRequest(
                RequestSchema: request.RequestSchema,
                Tenant: request.Tenant,
                RequestId: request.RequestId,
                RequestPath: request.RequestPath,
                QueryString: request.QueryString,
                Headers: request.Headers,
                ResourceName: request.ResourceName,
                Resource: request.Resource,
                ResourceId: request.ResourceId,
                TimeStamp: request.TimeStamp
            ), cancellationToken);
        }

        if (IfMatchPreconditionFailure(request.Headers, _previousResourceStore.VersionId,
                out var ifMatchPreconditionFailureFhirResourceResponse))
        {
            return ifMatchPreconditionFailureFhirResourceResponse ??
                   throw new NullReferenceException(nameof(ifMatchPreconditionFailureFhirResourceResponse));
        }

        if (request.Resource is Subscription subscription)
        {
            FhirOptionalResourceResponse? invalidSubscriptionUpdateResponse  = await ValidateSubscriptionUpdate(request, subscription);
            if (invalidSubscriptionUpdateResponse is not null)
            {
                return invalidSubscriptionUpdateResponse;
            }
        }

        IndexerOutcome indexerOutcome = await indexer.Process(request.Resource,
            fhirResourceTypeSupport.GetRequiredFhirResourceType(request.Resource.TypeName));

        int updatedVersionId = _previousResourceStore.VersionId + 1;
        SetResourceMeta(request.Resource, updatedVersionId, request.TimeStamp);

        var updatedResourceStore = new ResourceStore(
            resourceStoreId: null,
            resourceId: request.Resource.Id,
            versionId: updatedVersionId,
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

        _previousResourceStore.IsCurrent = false;
        await resourceStoreUpdate.Update(_previousResourceStore,
            deleteFhirIndexes: indexingSettingsOptions.Value.RemoveHistoricResourceIndexesOnUpdateOrDelete);
        updatedResourceStore = await resourceStoreAdd.Add(updatedResourceStore);

        AddRepositoryEvent(
            resourceType: updatedResourceStore.ResourceType,
            resourceId: updatedResourceStore.ResourceId,
            requestId: request.RequestId);

        //Refreshes the Subscription Cache if this was a successful Subscription registration 
        await ManageActiveSubscriptionCacheRefreshing(
            request:request,
            resourceId: updatedResourceStore.ResourceId,
            versionId: updatedResourceStore.VersionId);

        var responseHeaders = fhirResponseHttpHeaderSupport.ForUpdate(
            lastUpdatedUtc: updatedResourceStore.LastUpdatedUtc,
            versionId: updatedResourceStore.VersionId,
            requestTimeStamp: request.TimeStamp);

        return preferredReturnTypeService.GetResponse(
            httpStatusCode: HttpStatusCode.OK,
            resource: request.Resource,
            versionId: updatedResourceStore.VersionId,
            requestHeaders: request.Headers,
            responseHeaders: responseHeaders,
            repositoryEventQueue: repositoryEventCollector);
    }

    private async Task<FhirOptionalResourceResponse?> ValidateSubscriptionUpdate(
        FhirUpdateRequest request,
        Subscription subscription)
    {
        ArgumentNullException.ThrowIfNull(_previousResourceStore);

        //The system only sets Subscriptions statues to Error or Off, it never Activates them only users do via the FHIR API
        if (request is SystemSubscriptionUpdateRequest)
        {
            return null;
        }
        
        ResourceStore? previousSubscriptionResourceStore = await resourceStoreGetByResourceStoreId.Get(_previousResourceStore.ResourceStoreId!.Value);
        ArgumentNullException.ThrowIfNull(previousSubscriptionResourceStore);

        if (fhirDeSerializationSupport.ToResource(previousSubscriptionResourceStore.Json) is not Subscription previousSubscriptionResource)
        {
            throw new ApplicationException("The previous Subscription Resource json must be of type Subscription");
        }
        
        if (!AllowSubscriptionResourceUpdate(_previousResourceStore.IsDeleted, previousSubscriptionResource.Status, request))
        {
            return InvalidSubscriptionUpdateResponse();
        }
        
        _acceptSubscriptionOutcome = await fhirSubscriptionService.CanSubscriptionBeAccepted(subscription);
        if (_acceptSubscriptionOutcome is not null && !_acceptSubscriptionOutcome.Success)
        {
            return InvalidSubscriptionRegistrationResponse(_acceptSubscriptionOutcome);
        }

        return null;
    }

    private bool AllowSubscriptionResourceUpdate(
        bool isPreviousSubscriptionDeleted,
        Subscription.SubscriptionStatus? previousSubscriptionStatus,
        FhirUpdateRequest fhirUpdateRequest)
    {
        ArgumentNullException.ThrowIfNull(previousSubscriptionStatus);

        if (previousSubscriptionStatus == Subscription.SubscriptionStatus.Requested)
        {
            throw new ApplicationException("A stored Subscription resource must never have a status of Requested");
        }
        
        if (fhirUpdateRequest is SystemSubscriptionUpdateRequest)
        {
            return true;
        }

        if (isPreviousSubscriptionDeleted)
        {
            return true;
        }
        
        if (previousSubscriptionStatus != Subscription.SubscriptionStatus.Active )
        {
            return true;
        }

        return false;

    }

    private async Task ManageActiveSubscriptionCacheRefreshing(
        FhirUpdateRequest request,
        string resourceId,
        int versionId)
    {
        if (_acceptSubscriptionOutcome is not null && _acceptSubscriptionOutcome.Success || request is SystemSubscriptionUpdateRequest)
        {
            await activeSubscriptionCache.RefreshCache();
        }
        
        if (_acceptSubscriptionOutcome is not null && _acceptSubscriptionOutcome.Success)
        {
            logger.LogInformation("Subscription activated for resource Id: {ResourceID}, Version Id: {VersionId}",
                resourceId, versionId);
        }
    }

    private FhirOptionalResourceResponse InvalidValidatorResultResponse(
        ValidatorResult validatorResult)
    {
        repositoryEventCollector.Clear();
        return new FhirOptionalResourceResponse(
            Resource: validatorResult.GetOperationOutcome(),
            HttpStatusCode: validatorResult.GetHttpStatusCode(),
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }

    private FhirOptionalResourceResponse InvalidSubscriptionUpdateResponse()
    {
        repositoryEventCollector.Clear();
        return new FhirOptionalResourceResponse(
            Resource: operationOutcomeSupport.GetError(new[]
            {
                "FHIR Subscription resources are managed by the server. Only the status of Requested can be set by the " +
                "client either through a Create (POST) request or an Update (PUT) request, where the previous version was " +
                "in a status of Off or Error, or the previous resource was deleted. The client is not allowed to " +
                "update an Active Subscription resource, yet it can Delete an Active resource, thereby stopping any future Notifications." +
                "The Server is responsible for monitoring and activating Subscriptions resources to produce FHIR Notifications." +
                "Where a Subscriptions has encountered and error sending a notification, the status will be set to Error. Where " +
                "the Subscription continues to Error, it will eventually be set to Off and no more attempts wil be made.",
            }),
            HttpStatusCode: HttpStatusCode.BadRequest,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }

    private static void SetResourceMeta(
        Resource resource,
        int versionId,
        DateTimeOffset requestTimeStamp)
    {
        resource.Meta ??= new Meta();
        resource.Meta.VersionId = versionId.ToString();
        resource.Meta.LastUpdated = requestTimeStamp;
    }

    private bool IfMatchPreconditionFailure(
        Dictionary<string, StringValues> requestHeaders,
        int resourceStoreVersionId,
        out FhirOptionalResourceResponse? fhirResourceResponse)
    {
        repositoryEventCollector.Clear();
        fhirResourceResponse = null;
        int? ifMatchVersion = fhirRequestHttpHeaderSupport.GetIfMatch(requestHeaders);
        if (ifMatchVersion is null || resourceStoreVersionId == ifMatchVersion.Value)
        {
            return false;
        }

        fhirResourceResponse = new FhirOptionalResourceResponse(
            Resource: operationOutcomeSupport.GetError(
                new[]
                {
                    $"{HttpHeaderName.IfMatch} header precondition failure. Version update was for version {ifMatchVersion} however " +
                    $"the server found version {resourceStoreVersionId}. "
                }),
            HttpStatusCode: HttpStatusCode.PreconditionFailed,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
        return true;
    }

    private FhirOptionalResourceResponse InvalidSubscriptionRegistrationResponse(
        AcceptSubscriptionOutcome acceptSubscriptionOutcome)
    {
        repositoryEventCollector.Clear();
        return new FhirOptionalResourceResponse(
            Resource: acceptSubscriptionOutcome.OperationOutcome,
            HttpStatusCode: HttpStatusCode.BadRequest,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }

    private void AddRepositoryEvent(
        FhirResourceTypeId resourceType,
        string resourceId,
        string requestId)
    {
        ArgumentNullException.ThrowIfNull(resourceId);

        repositoryEventCollector.Add(
            resourceType: resourceType,
            requestId: requestId,
            repositoryEventType: RepositoryEventType.Update,
            resourceId: resourceId);
    }
}
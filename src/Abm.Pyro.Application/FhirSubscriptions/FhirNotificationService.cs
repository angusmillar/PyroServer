using System.Net;
using System.Net.Http.Headers;
using Abm.Pyro.Application.Cache;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirClient;
using Abm.Pyro.Application.FhirHandler;
using Abm.Pyro.Application.Notification;
using Abm.Pyro.Application.SearchQuery;
using Abm.Pyro.Application.Tenant;
using Abm.Pyro.Application.Validation;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Domain.Validation;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.FhirSubscriptions;

public class FhirNotificationService(
    ILogger<FhirNotificationService> logger,
    IValidator validator,
    IActiveSubscriptionCache activeSubscriptionCache,
    ISearchQueryService searchQueryService,
    IResourceStoreSearch resourceStoreSearch,
    IOperationOutcomeSupport operationOutcomeSupport,
    IFhirHttpClientFactory fhirHttpClientFactory,
    IFhirDeSerializationSupport fhirDeSerializationSupport,
    IFhirUpdateHandler fhirUpdateHandler,
    IFhirDeleteHandler fhirDeleteHandler,
    IResourceStoreGetByResourceId resourceStoreGetByResourceId,
    ITenantService tenantService,
    IDateTimeProvider dateTimeProvider) : IFhirNotificationService
{
    private ICollection<ActiveSubscription>? _activeSubscriptionList;
    private ICollection<ActiveSubscription>? _endDatedSubscriptionList;

    public async Task ProcessEventList(
        ICollection<RepositoryEvent> repositoryEventList,
        CancellationToken cancellationToken)
    {
        if (repositoryEventList.Count == 0)
        {
            return;
        }

        //The set of RepositoryEvents in the collection represents all events from a single inbound request on the
        //FHIR API, they MUST all have the same RequestId
        ThrowIfInvalidRequestIds(repositoryEventList);

        var fhirNotifiableEventList = GetFhirNotifiableEventList(repositoryEventList);
        if (fhirNotifiableEventList.Count == 0)
        {
            return;
        }

        _activeSubscriptionList = await activeSubscriptionCache.GetList();

        foreach (RepositoryEvent fhirNotifiableEvent in fhirNotifiableEventList)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await ProcessRepositoryEvent(fhirNotifiableEvent, cancellationToken);
        }
    }

    private async Task ProcessRepositoryEvent(
        RepositoryEvent repositoryEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_activeSubscriptionList);

        foreach (ActiveSubscription activeSubscription in _activeSubscriptionList.Where(x =>
                     x.CriteriaResourceType.Equals(repositoryEvent.ResourceType)))
        {
            await ProcessActiveSubscriptionForRepositoryEvent(repositoryEvent, cancellationToken, activeSubscription);
        }
    }

    private async Task ProcessActiveSubscriptionForRepositoryEvent(
        RepositoryEvent repositoryEvent,
        CancellationToken cancellationToken,
        ActiveSubscription activeSubscription)
    {
        if (!IsSupportedPayloadTypeOfFhirJson(activeSubscription.Payload))
        {
            throw new ApplicationException("Only FHIR JSON Payloads are supported");
        }


        if (await IsEndDatedSubscription(activeSubscription, cancellationToken))
        {
            return;
        }

        SearchQueryServiceOutcome searchQueryServiceOutcome = await GetSubscriptionCriteriaSearchQuery(
            eventResourceType: repositoryEvent.ResourceType,
            eventResourceID: repositoryEvent.ResourceId,
            activeSubscription: activeSubscription,
            cancellationToken: cancellationToken);

        ResourceStoreSearchOutcome resourceStoreSearchOutcome =
            await resourceStoreSearch.GetSearch(searchQueryServiceOutcome);

        ThrowIfMoreThanOneResourceFound(resourceStoreSearchOutcome);

        ResourceStore? resourceStore = resourceStoreSearchOutcome.ResourceStoreList.FirstOrDefault();

        if (resourceStore is null)
        {
            //No notification target resource were found for this RepositoryEvent, therefore nothing further to do 
            return;
        }

        Resource? resource = fhirDeSerializationSupport.ToResource(resourceStore.Json);

        ArgumentNullException.ThrowIfNull(resource);

        try
        {
            if (SendPayloadInNotification(activeSubscription.Payload))
            {
                await SendRestHookPutNotificationWithResource(cancellationToken, activeSubscription, resource);
                LogSuccess(repositoryEvent, activeSubscription);
                return;
            }

            await SendRestHookPostNotification(cancellationToken, activeSubscription);
            LogSuccess(repositoryEvent, activeSubscription);
        }
        catch (Exception exception) when (exception is HttpRequestException ||
                                   exception is TimeoutException ||
                                   exception is FhirOperationException)
        {
            var status = SubscriptionStatus.Error;
            await UpdateSubscriptionResourceStatus(
                resourceId: activeSubscription.ResourceId,
                versionId: activeSubscription.VersionId,
                status: status,
                statusReason:
                $"System set status to {status} due to failed notification sending attempts. {exception.Message}",
                cancellationToken: cancellationToken);

            logger.LogError(exception,
                "System set status to {Status} due to failed notification sending attempts",
                status.ToString());
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception when attempting to send a FHIR Notification");
            throw;
        }
    }

    
    private static void ThrowIfMoreThanOneResourceFound(
        ResourceStoreSearchOutcome resourceStoreSearchOutcome)
    {
        if (resourceStoreSearchOutcome.ResourceStoreList.Count > 1)
        {
            throw new ApplicationException(
                "Subscription notification criteria search result must contain exactly none or one resource store.");
        }
    }

    private static bool SendPayloadInNotification(
        string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        return payload.Equals(FhirFormatType.Json.GetDescription(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task SendRestHookPutNotificationWithResource(
        CancellationToken cancellationToken,
        ActiveSubscription activeSubscription,
        Resource resource)
    {
        Hl7.Fhir.Rest.FhirClient fhirClient =
            fhirHttpClientFactory.CreateFhirClient(baseUrl: activeSubscription.Endpoint.OriginalString);

        AddHeadersFromSubscription(activeSubscription.Headers, fhirClient.RequestHeaders);

        await fhirClient.UpdateAsync(resource: resource, false, cancellationToken);
    }
    
    private async Task SendRestHookPostNotification(
        CancellationToken cancellationToken,
        ActiveSubscription activeSubscription)
    {
        var basicHttpClient =
            fhirHttpClientFactory.CreateBasicClient(baseUrl: activeSubscription.Endpoint.OriginalString);

        AddHeadersFromSubscription(activeSubscription.Headers, basicHttpClient.DefaultRequestHeaders);

        await basicHttpClient.PostAsync(
            requestUri: activeSubscription.Endpoint.OriginalString,
            content: null,
            cancellationToken: cancellationToken);
    }

    private static void AddHeadersFromSubscription(
        string[] subscriptionHeaders,
        HttpRequestHeaders? httpRequestHeaders)
    {
        ArgumentNullException.ThrowIfNull(httpRequestHeaders);

        foreach (string header in subscriptionHeaders)
        {
            string[] headerSplit = header.Split(":");
            if (headerSplit.Length == 2)
            {
                httpRequestHeaders.Add(headerSplit[0].Trim(), headerSplit[1].Trim());
                continue;
            }

            httpRequestHeaders.Add(headerSplit[0].Trim(), string.Empty);
        }
    }

    private async Task<SearchQueryServiceOutcome> GetSubscriptionCriteriaSearchQuery(
        FhirResourceTypeId eventResourceType,
        string eventResourceID,
        ActiveSubscription activeSubscription,
        CancellationToken cancellationToken)
    {
        string resourceIdEnhancedCriteria = $"_id={eventResourceID}&{activeSubscription.CriteriaQuery}";
        SearchQueryServiceOutcome searchQueryServiceOutcome =
            await searchQueryService.Process(eventResourceType, resourceIdEnhancedCriteria);

        ValidatorResult searchQueryValidatorResult = validator.Validate(new SearchQueryServiceOutcomeAndHeaders(
            SearchQueryServiceOutcome: searchQueryServiceOutcome,
            Headers: new Dictionary<string, StringValues>()));

        if (searchQueryValidatorResult.IsValid)
        {
            return searchQueryServiceOutcome;
        }

        string searchQueryValidatorErrorMessage = String.Join(", ",
            operationOutcomeSupport.ExtractErrorMessages(searchQueryValidatorResult.GetOperationOutcome()));

        await UpdateSubscriptionResourceStatus(
            resourceId: activeSubscription.ResourceId,
            versionId: activeSubscription.VersionId,
            status: SubscriptionStatus.Error,
            statusReason:
            $"The Subscription.criteria failed validation while responding to a possible notification event. {searchQueryValidatorErrorMessage}",
            cancellationToken: cancellationToken);

        return searchQueryServiceOutcome;
    }

    private static bool IsSupportedPayloadTypeOfFhirJson(
        string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return true;
        }

        return payload.Equals(FhirFormatType.Json.GetDescription(), StringComparison.OrdinalIgnoreCase);
    }

    private static void ThrowIfInvalidRequestIds(
        ICollection<RepositoryEvent> repositoryEventList)
    {
        if (!repositoryEventList.All(x => x.RequestId.Equals(repositoryEventList.First().RequestId)))
        {
            throw new ApplicationException("All Repository Events in the collection must have the same RequestId");
        }
    }

    private static List<RepositoryEvent> GetFhirNotifiableEventList(
        ICollection<RepositoryEvent> repositoryEventList)
    {
        RepositoryEventType[] fhirNotifiableEvents = [RepositoryEventType.Create, RepositoryEventType.Update];
        return repositoryEventList.Where(x => fhirNotifiableEvents.Contains(x.RepositoryEventType)).ToList();
    }

    public enum SubscriptionStatus
    {
        Error,
        Off
    }

    public async Task UpdateSubscriptionResourceStatus(
        string resourceId,
        int versionId,
        SubscriptionStatus status,
        string? statusReason,
        CancellationToken cancellationToken)
    {
        ResourceStore? resourceStore = await resourceStoreGetByResourceId.Get(
            resourceType: FhirResourceTypeId.Subscription,
            resourceId: resourceId);

        if (resourceStore is null)
        {
            logger.LogError(
                "Unable to update the status of a Subscription resource to error, as the resource is not found " +
                "by its resourceId: {ResourceId}. The status reason was to be: {StatusReason} ", resourceId,
                statusReason);
            return;
        }
        
        if (resourceStore.VersionId != versionId)
        {
            logger.LogWarning("The Subscription Resource Id: {ResourceId} acted upon is different from the current Active Subscription Version. " +
                              "Unable to record notification failure as the version being acted upon was different to the current active version. " +
                              "This failure wil be ignored as new notifications will act upon the newer active Subscription resource. " +
                              "This VersionId: {OldVersionId}, New Version: {NewVersionId}",
                resourceId,
                versionId,
                resourceStore.VersionId);
            
            return;
        }

        Resource? resource = fhirDeSerializationSupport.ToResource(resourceStore.Json);
        if (resource is not Subscription subscription)
        {
            throw new InvalidCastException(nameof(resource));
        }

        subscription.Status = status switch
        {
            SubscriptionStatus.Error => Subscription.SubscriptionStatus.Error,
            SubscriptionStatus.Off => Subscription.SubscriptionStatus.Off,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

        subscription.Error = statusReason;

        await fhirUpdateHandler.Handle(
            tenant: tenantService.GetScopedTenant().Code,
            requestId: $"System:{GuidSupport.NewFhirGuid()}",
            resourceId: resourceId,
            resource: resource,
            headers: new Dictionary<string, StringValues>(),
            cancellationToken: cancellationToken);
    }

    private async Task<bool> IsEndDatedSubscription(
        ActiveSubscription activeSubscription,
        CancellationToken cancellationToken)
    {
        if (_endDatedSubscriptionList is not null && !_endDatedSubscriptionList.Contains(activeSubscription))
        {
            return true;
        }

        if (IsSubscriptionEndDated(activeSubscription.EndDateTime))
        {
            await fhirDeleteHandler.Handle(
                tenant: tenantService.GetScopedTenant().Code,
                requestId: $"System:{GuidSupport.NewFhirGuid()}",
                resourceName: FhirResourceTypeId.Subscription.GetCode(),
                resourceId: activeSubscription.ResourceId,
                cancellationToken: cancellationToken);

            _endDatedSubscriptionList ??= new List<ActiveSubscription>();
            _endDatedSubscriptionList.Add(activeSubscription);

            return true;
        }

        return false;
    }

    private bool IsSubscriptionEndDated(
        DateTimeOffset? subscriptionEnd)
    {
        return subscriptionEnd.HasValue && subscriptionEnd.Value.UtcDateTime < dateTimeProvider.Now.UtcDateTime;
    }

    private void LogSuccess(
        RepositoryEvent repositoryEvent,
        ActiveSubscription activeSubscription)
    {
        logger.LogDebug(
            "FHIR Notification triggered for Tenant {Tenant} Request Id {RequestId} against SubscriptionId {SubscriptionId} for Resource {ResourceName}/{ResourceId} due to Repository event {RepositoryEventType}",
            repositoryEvent.Tenant.Code,
            repositoryEvent.RequestId,
            activeSubscription.ResourceId,
            repositoryEvent.ResourceType.GetCode(),
            repositoryEvent.ResourceId,
            repositoryEvent.RepositoryEventType.GetCode());
    }
}
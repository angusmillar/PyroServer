using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirSubscriptions;
using Abm.Pyro.Application.FhirValidateService;
using Abm.Pyro.Application.Indexing;
using Hl7.Fhir.Model;
using Abm.Pyro.Domain.Dispatcher;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.FhirSubscriptions;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Indexing;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Notification;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.Validation;
using Microsoft.Extensions.Logging;
using SummaryType = Hl7.Fhir.Rest.SummaryType;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirCreateHandler(
    ILogger<FhirCreateHandler> logger,
    IValidator validator,
    IResourceStoreAdd resourceStoreAdd,
    IFhirSerializationSupport fhirSerializationSupport,
    IFhirResourceTypeSupport fhirResourceTypeSupport,
    IFhirResponseHttpHeaderSupport fhirResponseHttpHeaderSupport,
    IIndexer indexer,
    IPreferredReturnTypeService preferredReturnTypeService,
    IServiceBaseUrlCache serviceBaseUrlCache,
    IRepositoryEventCollector repositoryEventCollector,
    IActiveSubscriptionCache activeSubscriptionCache,
    IFhirSubscriptionService fhirSubscriptionService,
    IServiceSettingsCache serviceSettingsCache,
    IFhirValidateEngine fhirValidateEngine)
    : IRequestHandler<FhirCreateRequest, FhirOptionalResourceResponse>, IFhirCreateHandler
{

    private AcceptSubscriptionOutcome? _acceptSubscriptionOutcome;
    
    public Task<FhirOptionalResourceResponse> Handle(
        string tenant, 
        string requestId, 
        string resourceId, 
        Resource resource, 
        Dictionary<string, StringValues> headers, 
        CancellationToken cancellationToken)
    {
        return Handle(new FhirCreateRequest(
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
        
        //FHIR profile validation if enabled
        if ((await serviceSettingsCache.GetFhirValidationSettings()).ValidateOnCreate)
        {
            OperationOutcome operationOutcome = await fhirValidateEngine.Validate(request.Resource);
            if (!operationOutcome.Success)
            {
                return InvalidFhirProfileValidationResultResponse(operationOutcome);
            }
        }

        //Manage the acceptance or rejection of requests to create new active Subscriptions
        if (request.Resource is Subscription subscription)
        {
            _acceptSubscriptionOutcome = await fhirSubscriptionService.CanSubscriptionBeAccepted(subscription);
            if (_acceptSubscriptionOutcome is not null && !_acceptSubscriptionOutcome.Success)
            {
                return InvalidSubscriptionRegistrationResponse(_acceptSubscriptionOutcome);
            }
        }

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

        AddRepositoryCreateEvent(
            resourceType:resourceStore.ResourceType, 
            resourceId: resourceStore.ResourceId);


        //Refreshes the Subscription Cache if this was a successful Subscription registration 
        await ManageActiveSubscriptionCacheRefreshing(
            resourceId: resourceStore.ResourceId, 
            versionId: resourceStore.VersionId);
        

        ServiceBaseUrl serviceBaseUrl = await serviceBaseUrlCache.GetRequiredPrimaryAsync();
        
        var responseHeaders = fhirResponseHttpHeaderSupport.ForCreate(
            resourceType: resourceStore.ResourceType,
            lastUpdatedUtc: resourceStore.LastUpdatedUtc,
            resourceId: resourceStore.ResourceId,
            versionId: resourceStore.VersionId,
            requestTimeStamp: request.TimeStamp,
            requestSchema: request.RequestSchema,
            serviceBaseUrl: serviceBaseUrl.Url);

        return preferredReturnTypeService.GetResponse(
            httpStatusCode: HttpStatusCode.Created, 
            resource: request.Resource,
            versionId: resourceStore.VersionId, 
            requestHeaders: request.Headers, 
            responseHeaders: responseHeaders, 
            repositoryEventQueue: repositoryEventCollector);
    }

    private async Task ManageActiveSubscriptionCacheRefreshing(string resourceId, int versionId)
    {
        if (_acceptSubscriptionOutcome is not null && _acceptSubscriptionOutcome.Success)
        {
            await activeSubscriptionCache.RefreshCache();
            logger.LogInformation("Subscription activated for resource Id: {ResourceID}, Version Id: {VersionId}", resourceId, versionId);
        }
    }

    private void AddRepositoryCreateEvent(FhirResourceTypeId resourceType,  string resourceId)
    {
        repositoryEventCollector.Add(
            resourceType: resourceType,
            repositoryEventType: RepositoryEventType.Create,
            resourceId: resourceId);
    }

    private FhirOptionalResourceResponse InvalidFhirProfileValidationResultResponse(OperationOutcome operationOutcome)
    {
        repositoryEventCollector.Clear();
        return new FhirOptionalResourceResponse(
            Resource: operationOutcome, 
            HttpStatusCode: HttpStatusCode.BadRequest,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }
    
    private FhirOptionalResourceResponse InvalidValidatorResultResponse(ValidatorResult validatorResult)
    {
        repositoryEventCollector.Clear();
        return new FhirOptionalResourceResponse(
            Resource: validatorResult.GetOperationOutcome(), 
            HttpStatusCode: validatorResult.GetHttpStatusCode(),
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }
    
    private FhirOptionalResourceResponse InvalidSubscriptionRegistrationResponse(AcceptSubscriptionOutcome acceptSubscriptionOutcome)
    {
        repositoryEventCollector.Clear();
        return new FhirOptionalResourceResponse(
            Resource: acceptSubscriptionOutcome.OperationOutcome, 
            HttpStatusCode: HttpStatusCode.BadRequest,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }

    private static void SetResourceMeta(Resource resource,
        DateTimeOffset requestTimeStamp)
    {
        resource.Meta ??= new Meta();
        resource.Meta.VersionId = "1";
        resource.Meta.LastUpdated = requestTimeStamp;
    }
}
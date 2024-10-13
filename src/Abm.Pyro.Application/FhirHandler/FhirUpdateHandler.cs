using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
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
using SummaryType = Hl7.Fhir.Rest.SummaryType;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirUpdateHandler(
  IValidator validator,
  IFhirResourceTypeSupport fhirResourceTypeSupport,
  IResourceStoreGetForUpdateByResourceId resourceStoreGetForUpdateByResourceId,
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
  IRepositoryEventCollector repositoryEventCollector)
  : IRequestHandler<FhirUpdateRequest, FhirOptionalResourceResponse>, IFhirUpdateHandler
{
  private ResourceStoreUpdateProjection? PreviousResourceStore;
  public async Task<FhirOptionalResourceResponse> Handle(
    string tenant,
    string requestId,
    string resourceId, 
    Resource resource, 
    Dictionary<string, StringValues> headers, 
    CancellationToken cancellationToken, 
    ResourceStoreUpdateProjection? previousResourceStore = null)
  {
    PreviousResourceStore = previousResourceStore;
    
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
  
  public async Task<FhirOptionalResourceResponse> Handle(FhirUpdateRequest request, CancellationToken cancellationToken)
  {
    ValidatorResult validatorResult = validator.Validate(request);
    if (!validatorResult.IsValid)
    {
      return InvalidValidatorResultResponse(validatorResult);
    }
    
    FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(request.Resource.TypeName);
    
    if (PreviousResourceStore is null)
    {
      PreviousResourceStore = await resourceStoreGetForUpdateByResourceId.Get(fhirResourceType, request.Resource.Id);  
    }
    
    if (PreviousResourceStore is null)
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

    if (fhirResourceType is FhirResourceTypeId.Subscription)
    {
      return InvalidSubscriptionUpdateResponse();
    }
    
    if (IfMatchPreconditionFailure(request.Headers, PreviousResourceStore.VersionId, out var ifMatchPreconditionFailureFhirResourceResponse))
    {
      return ifMatchPreconditionFailureFhirResourceResponse ?? throw new NullReferenceException(nameof(ifMatchPreconditionFailureFhirResourceResponse));
    }
    
    IndexerOutcome indexerOutcome = await indexer.Process(request.Resource, fhirResourceTypeSupport.GetRequiredFhirResourceType(request.Resource.TypeName));
    
    int updatedVersionId = PreviousResourceStore.VersionId + 1;
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
    
    PreviousResourceStore.IsCurrent = false;
    await resourceStoreUpdate.Update(PreviousResourceStore, deleteFhirIndexes: indexingSettingsOptions.Value.RemoveHistoricResourceIndexesOnUpdateOrDelete);
    updatedResourceStore = await resourceStoreAdd.Add(updatedResourceStore);

    AddRepositoryEvent(
      resourceType: updatedResourceStore.ResourceType, 
      resourceId: updatedResourceStore.ResourceId, 
      requestId: request.RequestId);
    
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

  private FhirOptionalResourceResponse InvalidValidatorResultResponse(ValidatorResult validatorResult)
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
      Resource: operationOutcomeSupport.GetError(new []
      {
        "FHIR Subscription resources can only be created via POST requests or PUT (Update as Create) requests, and later Deleted by the Client. ",
        "The Server is responsible for monitoring and activating Subscriptions to produce FHIR Notifications." +
        "Clients can not Update Subscriptions resource, they can only Create and Delete them.",
      }), 
      HttpStatusCode: HttpStatusCode.BadRequest,
      Headers: new Dictionary<string, StringValues>(),
      RepositoryEventCollector: repositoryEventCollector);
  }
  
  private static void SetResourceMeta(Resource resource, int versionId, DateTimeOffset requestTimeStamp)
  {
    resource.Meta ??= new Meta();
    resource.Meta.VersionId = versionId.ToString();
    resource.Meta.LastUpdated = requestTimeStamp;
  }

  private bool IfMatchPreconditionFailure(Dictionary<string, StringValues> requestHeaders, int resourceStoreVersionId, out FhirOptionalResourceResponse? fhirResourceResponse)
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
        new[] {
          $"{HttpHeaderName.IfMatch} header precondition failure. Version update was for version {ifMatchVersion} however " +
          $"the server found version {resourceStoreVersionId}. "
        }),
      HttpStatusCode: HttpStatusCode.PreconditionFailed,
      Headers: new Dictionary<string, StringValues>(),
      RepositoryEventCollector: repositoryEventCollector);
    return true;
  }
  
  private void AddRepositoryEvent(FhirResourceTypeId resourceType, string resourceId, string requestId)
  {
    ArgumentNullException.ThrowIfNull(resourceId);
            
    repositoryEventCollector.Add(
      resourceType: resourceType,
      requestId: requestId,
      repositoryEventType: RepositoryEventType.Update, 
      resourceId: resourceId);
  }
}

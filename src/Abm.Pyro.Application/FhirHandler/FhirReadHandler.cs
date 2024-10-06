using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.Notification;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Application.Validation;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirReadHandler(
  IValidator validator,
  IResourceStoreGetByResourceId resourceStoreGetByResourceId,
  IFhirResponseHttpHeaderSupport fhirResponseHttpHeaderSupport,
  IFhirResourceTypeSupport fhirResourceTypeSupport,
  IFhirDeSerializationSupport fhirDeSerializationSupport,
  IFhirRequestHttpHeaderSupport fhirRequestHttpHeaderSupport,
  IRepositoryEventCollector repositoryEventCollector)
  : IRequestHandler<FhirReadRequest, FhirOptionalResourceResponse>, IFhirReadHandler
{
  public async Task<FhirOptionalResourceResponse> Handle(string tenant, string requestId, string resourceName, string resourceId, CancellationToken cancellationToken, Dictionary<string, StringValues>? headers = null)
  {
    if (headers is null)
    {
      headers = new Dictionary<string, StringValues>();
    }
    
    return await Handle(new FhirReadRequest(
      RequestSchema: "http",
      Tenant: tenant,
      RequestId: requestId,
      RequestPath: string.Empty,
      QueryString: null,
      Headers: headers,
      ResourceName: resourceName,
      ResourceId: resourceId,
      TimeStamp: DateTimeOffset.Now), cancellationToken: cancellationToken);
  }
  
  public async Task<FhirOptionalResourceResponse> Handle(
    FhirReadRequest request, 
    CancellationToken cancellationToken)
  {
    ValidatorResult validatorResult = validator.Validate(request);
    if (!validatorResult.IsValid)
    {
      return InvalidValidatorResultResponse(validatorResult);
    }
    
    FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(request.ResourceName);

    ResourceStore? resourceStore = await resourceStoreGetByResourceId.Get(request.ResourceId, fhirResourceType);
    
    if (resourceStore is null)
    {
      repositoryEventCollector.Clear();
      return new FhirOptionalResourceResponse(
        Resource: null, 
        HttpStatusCode: HttpStatusCode.NotFound, 
        Headers: new Dictionary<string, StringValues>(),
        RepositoryEventCollector: repositoryEventCollector);
    }
    
    if (!IfNoneMatch(request.Headers, resourceStore))
    {
      return NotModifiedResponse();
    }
    
    if (!IfModifiedSince(request.Headers, resourceStore.LastUpdatedUtc))
    {
      return NotModifiedResponse();
    }
    
    var headers = fhirResponseHttpHeaderSupport.ForRead(
      lastUpdatedUtc: resourceStore.LastUpdatedUtc, 
      versionId: resourceStore.VersionId, 
      requestTimeStamp: request.TimeStamp);
    
    if (resourceStore.IsDeleted)
    {
      repositoryEventCollector.Clear();
      return new FhirOptionalResourceResponse(
        Resource: null, 
        HttpStatusCode: HttpStatusCode.Gone, 
        Headers: headers,
        RepositoryEventCollector: repositoryEventCollector);
    }

    AddRepositoryEvent(resourceStore.ResourceStoreId, request.RequestId);
    
    Resource? resource = fhirDeSerializationSupport.ToResource(resourceStore.Json);
    return new FhirOptionalResourceResponse(
      Resource: resource, 
      HttpStatusCode: HttpStatusCode.OK, 
      Headers: headers, 
      RepositoryEventCollector: repositoryEventCollector,
      ResourceOutcomeInfo: new ResourceOutcomeInfo(
        resourceId: resourceStore.ResourceId, 
        versionId: resourceStore.VersionId));
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

  private bool IfNoneMatch(Dictionary<string, StringValues> requestHeaders, ResourceStore resourceStore)
  {
    int? ifNoneMatchVersionId = fhirRequestHttpHeaderSupport.GetIfNoneMatch(requestHeaders);
    if (ifNoneMatchVersionId is null)
    {
      return true;
    }
    return resourceStore.VersionId != ifNoneMatchVersionId;
  }

  private bool IfModifiedSince(Dictionary<string, StringValues> requestHeaders, DateTime resourceStoreLastUpdatedUtc)
  {
    DateTime? ifModifiedSinceUtc = fhirRequestHttpHeaderSupport.GetIfModifiedSince(requestHeaders);
    if (ifModifiedSinceUtc is null)
    {
      return true;
    }
    return resourceStoreLastUpdatedUtc > ifModifiedSinceUtc;
  }

  private FhirOptionalResourceResponse NotModifiedResponse()
  {
    repositoryEventCollector.Clear();
    return new FhirOptionalResourceResponse(
      Resource: null, 
      HttpStatusCode: HttpStatusCode.NotModified, 
      Headers: new Dictionary<string, StringValues>(),
      RepositoryEventCollector: repositoryEventCollector);
  }

  private void AddRepositoryEvent(int? resourceStoreId, string requestId)
  {
    ArgumentNullException.ThrowIfNull(resourceStoreId);

    repositoryEventCollector.Add(
      requestId: requestId,
      repositoryEventType: RepositoryEventType.Read, 
      resourceStoreId: resourceStoreId.Value);
  }
}

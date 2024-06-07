using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Application.Validation;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirReadHandler(
  IValidator validator,
  IResourceStoreGetByResourceId resourceStoreGetByResourceId,
  IFhirResponseHttpHeaderSupport fhirResponseHttpHeaderSupport,
  IFhirResourceTypeSupport fhirResourceTypeSupport,
  IFhirDeSerializationSupport fhirDeSerializationSupport,
  IFhirRequestHttpHeaderSupport fhirRequestHttpHeaderSupport)
  : IRequestHandler<FhirReadRequest, FhirOptionalResourceResponse>, IFhirReadHandler
{
  public async Task<FhirOptionalResourceResponse> Handle(string resourceName, string resourceId, CancellationToken cancellationToken, Dictionary<string, StringValues>? headers = null)
  {
    if (headers is null)
    {
      headers = new Dictionary<string, StringValues>();
    }
    
    return await Handle(new FhirReadRequest(
      RequestSchema: "http",
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
      return new FhirOptionalResourceResponse(Resource: null, HttpStatusCode: HttpStatusCode.NotFound, Headers: new Dictionary<string, StringValues>());
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
      return new FhirOptionalResourceResponse(Resource: null, HttpStatusCode: HttpStatusCode.Gone, Headers: headers);
    }

    Resource? resource = fhirDeSerializationSupport.ToResource(resourceStore.Json);
    return new FhirOptionalResourceResponse(
      Resource: resource, 
      HttpStatusCode: HttpStatusCode.OK, 
      Headers: headers, 
      ResourceOutcomeInfo: new ResourceOutcomeInfo(
        resourceId: resourceStore.ResourceId, 
        versionId: resourceStore.VersionId));
  }
  
  private static FhirOptionalResourceResponse InvalidValidatorResultResponse(ValidatorResult validatorResult)
  {
    return new FhirOptionalResourceResponse(
      Resource: validatorResult.GetOperationOutcome(), 
      HttpStatusCode: validatorResult.GetHttpStatusCode(),
      Headers: new Dictionary<string, StringValues>());
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

  private static FhirOptionalResourceResponse NotModifiedResponse()
  {
    return new FhirOptionalResourceResponse(Resource: null, HttpStatusCode: HttpStatusCode.NotModified, Headers: new Dictionary<string, StringValues>());
  }
}

using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using MediatR;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirHandler;
public class FhirDeleteHandler(
  IValidator validator,
  IResourceStoreGetForUpdateByResourceId resourceStoreGetForUpdateByResourceId,
  IFhirResponseHttpHeaderSupport fhirResponseHttpHeaderSupport,
  IResourceStoreUpdate resourceStoreUpdate,
  IResourceStoreAdd resourceStoreAdd,
  IFhirResourceTypeSupport fhirResourceTypeSupport,
  IOptions<IndexingSettings> indexingSettingsOptions)
  : IRequestHandler<FhirDeleteRequest, FhirOptionalResourceResponse>, IFhirDeleteHandler
{

  private ResourceStoreUpdateProjection? PreviousResourceStore;
  
  public async Task<FhirOptionalResourceResponse> Handle(string tenant, string resourceName, string resourceId, CancellationToken cancellationToken, ResourceStoreUpdateProjection? previousResourceStore = null)
  {
    PreviousResourceStore = previousResourceStore;
    
    return await Handle(new FhirDeleteRequest(
      RequestSchema: "http",
      tenant: tenant,
      RequestPath: string.Empty,
      QueryString: null,
      Headers: new Dictionary<string, StringValues>(),
      ResourceName: resourceName,
      ResourceId: resourceId,
      TimeStamp: DateTimeOffset.Now), cancellationToken: cancellationToken);
  }

  public async Task<FhirOptionalResourceResponse> Handle(FhirDeleteRequest request, CancellationToken cancellationToken)
  {
    ValidatorResult validatorResult = validator.Validate(request);
    if (!validatorResult.IsValid)
    {
      return InvalidValidatorResultResponse(validatorResult);
    }
    
    FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(request.ResourceName);


    if (PreviousResourceStore is null)
    {
      PreviousResourceStore = await resourceStoreGetForUpdateByResourceId.Get(fhirResourceType, request.ResourceId);  
    }
    
    if (PreviousResourceStore is null)
    {
      return new FhirOptionalResourceResponse(null, HttpStatusCode.NoContent, new Dictionary<string, StringValues>());
    }
    
    if (PreviousResourceStore.IsDeleted)
    {
      return new FhirOptionalResourceResponse(
        Resource: null, 
        HttpStatusCode: HttpStatusCode.NoContent, 
        Headers:fhirResponseHttpHeaderSupport.ForDelete(
          requestTimeStamp: request.TimeStamp, 
          versionId: PreviousResourceStore.VersionId), 
        ResourceOutcomeInfo: new ResourceOutcomeInfo(
          resourceId: request.ResourceId, 
          versionId: PreviousResourceStore.VersionId));
    }
    
    var deletedResourceStore = new ResourceStore(
      resourceStoreId: null,
      resourceId: request.ResourceId,
      versionId: PreviousResourceStore.VersionId + 1,
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
    
    PreviousResourceStore.IsCurrent = false;
    await resourceStoreUpdate.Update(PreviousResourceStore, indexingSettingsOptions.Value.RemoveHistoricResourceIndexesOnUpdateOrDelete);
    deletedResourceStore = await resourceStoreAdd.Add(deletedResourceStore);
    
    return new FhirOptionalResourceResponse(
      Resource: null, 
      HttpStatusCode: HttpStatusCode.NoContent, 
      Headers:fhirResponseHttpHeaderSupport.ForDelete(
        requestTimeStamp: request.TimeStamp, 
        versionId: deletedResourceStore.VersionId), 
      ResourceOutcomeInfo: new ResourceOutcomeInfo(
        resourceId: deletedResourceStore.ResourceId, 
        versionId: deletedResourceStore.VersionId));
  }
  
  private static FhirOptionalResourceResponse InvalidValidatorResultResponse(ValidatorResult validatorResult)
  {
    return new FhirOptionalResourceResponse(
      Resource: validatorResult.GetOperationOutcome(), 
      HttpStatusCode: validatorResult.GetHttpStatusCode(),
      Headers: new Dictionary<string, StringValues>());
  }
}

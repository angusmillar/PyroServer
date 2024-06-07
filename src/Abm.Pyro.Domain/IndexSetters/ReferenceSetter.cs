using System.Net;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Exceptions;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;
using Abm.Pyro.Domain.Support;
using FhirUri = Hl7.Fhir.Model.FhirUri;

namespace Abm.Pyro.Domain.IndexSetters;

public class ReferenceSetter(
  IFhirUriFactory fhirUriFactory,
  IFhirResourceTypeSupport fhrFhirResourceTypeSupport,
  IServiceBaseUrlAddByUri serviceBaseUrlAddByUri,
  IServiceBaseUrlCache serviceBaseUrlCache,
  ILogger<ReferenceSetter> logger,
  IServiceBaseUrlUpdateSimultaneous serviceBaseUrlUpdateSimultaneous,
  IServiceBaseUrlUpdate serviceBaseUrlUpdate)
  : IReferenceSetter
{
  private readonly IServiceBaseUrlUpdate ServiceBaseUrlUpdate = serviceBaseUrlUpdate;
  private readonly IServiceBaseUrlUpdateSimultaneous ServiceBaseUrlUpdateSimultaneous = serviceBaseUrlUpdateSimultaneous;
  private readonly ILogger<ReferenceSetter> Logger = logger;

  private FhirResourceTypeId ResourceType;
  private int SearchParameterId;
  private string? SearchParameterName;

  public async Task<IList<IndexReference>> SetAsync(ITypedElement typedElement, FhirResourceTypeId resourceType, int searchParameterId, string searchParameterName)
  {
    ResourceType = resourceType;
    SearchParameterId = searchParameterId;
    SearchParameterName = searchParameterName;

    if (typedElement is ScopedNode scopedNode && scopedNode.Current is IFhirValueProvider fhirValueProvider)
    {
      if (fhirValueProvider.FhirValue is null)
      {
        throw new NullReferenceException($"FhirValueProvider's FhirValue found to be null for the SearchParameter entity with the database " +
                                         $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                         $"name of: {SearchParameterName}");
      }

      return await ProcessFhirDataType(fhirValueProvider.FhirValue);
    }

    if (typedElement.Value is null)
    {
      throw new NullReferenceException($"ITypedElement's Value found to be null for the SearchParameter entity with the database " +
                                       $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                       $"name of: {SearchParameterName}");
    }
    
    return ProcessPrimitiveDataType(typedElement.Value);
  }

  private IList<IndexReference> ProcessPrimitiveDataType(object obj)
  {
    switch (obj)
    {
      default:
        throw new FormatException($"Unknown Primitive DataType: {obj.GetType().Name} for the SearchParameter entity with the database " +
                                  $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                  $"name of: {SearchParameterName}");

    }
  }
  
  private async Task<IList<IndexReference>> ProcessFhirDataType(Base fhirValue)
  {
    switch (fhirValue)
    {
      case FhirUri fhirUri:
        return await SetFhirUri(fhirUri);
      case ResourceReference resourceReference:
        return await SetResourceReference(resourceReference);
      case Canonical canonical:
        return await SetCanonical(canonical);
      case Resource resource:
        return SetResource(resource);
      case Attachment attachment:
        return await SetUri(attachment);
      case Identifier identifier:
        return await SetIdentifier(identifier);
      default:
        throw new FormatException($"Unknown FhirType: {fhirValue.GetType().Name} for the SearchParameter entity with the database " +
                                  $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                  $"name of: {SearchParameterName}");
    }
  }
  
  private async Task<IList<IndexReference>> SetCanonical(Canonical canonical)
  {
    if (string.IsNullOrWhiteSpace(canonical.Value))
    {
      return Array.Empty<IndexReference>();
    }
    return await SetReference(canonical.Value);
  }

  private async Task<IList<IndexReference>> SetIdentifier(Identifier identifier)
  {
    if (!string.IsNullOrWhiteSpace(identifier.System) && string.IsNullOrWhiteSpace(identifier.Value))
    {
      return Array.Empty<IndexReference>();
    }
    return await SetReference($"{identifier.System}/{identifier.Value}");
  }

  private static IList<IndexReference> SetResource(Resource resource)
  {
    if (resource.TypeName == FhirResourceTypeId.Composition.GetCode() || resource.TypeName == FhirResourceTypeId.MessageHeader.GetCode())
    {
      //
      //
      //ToDo: What do we do with this Resource as a ResourceReference??
      //FHIR Spec says:
      //The first resource in the bundle, if the bundle type is "document" - this is a composition, and this parameter provides access to searches its contents
      //and
      //The first resource in the bundle, if the bundle type is "message" - this is a message header, and this parameter provides access to search its contents

      //So the intent is that search parameter 'composition' and 'message' are to work like chain parameters yet the chain reaches into the 
      //first resource of the bundle which should be a Composition  Resource or and MessageHeader resource.
      //Yet, unlike chaining where the reference points to the endpoint for that Resource type, here the reference points to the first entry of the bundle endpoint.
      // It almost feels like we should index at the bundle endpoint all the search parameters for both the Composition  and MessageHeader resources.
      //Or maybe we do special processing on Bundle commits were we pull out the first resource and store it at the appropriate 
      //endpoint yet hind it from access, then provide a reference index type at the bundle endpoint that chains to these 
      //hidden resources
    }
    return Array.Empty<IndexReference>();
  }

  private async Task<IList<IndexReference>> SetResourceReference(ResourceReference resourceReference)
  {
    //Check the Uri is actual a Fhir resource reference 
    if (!HttpUtil.IsRestResourceIdentity(resourceReference.Reference))
    {
      return Array.Empty<IndexReference>();
    }
    if (resourceReference.IsContainedReference || resourceReference.Url is null)
    {
      return Array.Empty<IndexReference>();
    }
    return await SetReference(resourceReference.Url.OriginalString);
  }

  private async Task<IList<IndexReference>> SetUri(Attachment attachment)
  {
    if (string.IsNullOrWhiteSpace(attachment.Url))
    {
      return Array.Empty<IndexReference>();
    }
    return await SetReference(attachment.Url);
  }

  private async Task<IList<IndexReference>> SetFhirUri(FhirUri fhirUri)
  {
    if (!string.IsNullOrWhiteSpace(fhirUri.Value))
    {
      return await SetReference(fhirUri.Value);
    }
    return Array.Empty<IndexReference>();
  }

  private async Task<IList<IndexReference>> SetReference(string uriString)
  {
    //Check the Uri is actual a Fhir resource reference         
    if (!HttpUtil.IsRestResourceIdentity(uriString))
    {
      return Array.Empty<IndexReference>();
    }

    if (!fhirUriFactory.TryParse(uriString.Trim(), out FhirSupport.FhirUri? referenceUri, errorMessage: out string errorMessage))
    {
      string message = $"One of the resources references found in the submitted resource is invalid. The reference was : {uriString}. The error was: {errorMessage}";
      throw new FhirErrorException(HttpStatusCode.BadRequest, new[] { message });
    }

    if (referenceUri.UriPrimaryServiceRoot is not null)
    {
      ServiceBaseUrl serviceBaseUrl = await GetServiceBaseUrl(referenceUri);
      
      var resourceIndex = new IndexReference(
        indexReferenceId: null,
        resourceStoreId: null,
        resourceStore: null,
        searchParameterStoreId: SearchParameterId,
        searchParameterStore: null,
        resourceType: fhrFhirResourceTypeSupport.GetRequiredFhirResourceType(referenceUri.ResourceName),
        serviceBaseUrlId: serviceBaseUrl.ServiceBaseUrlId,
        serviceBaseUrl: null,
        resourceId: referenceUri.ResourceId,
        versionId: referenceUri.VersionId.NullIfEmptyString(),
        canonicalVersionId: referenceUri.CanonicalVersionId.NullIfEmptyString());

      return new List<IndexReference> { resourceIndex };
    }
    return Array.Empty<IndexReference>();
  }

  private async Task<ServiceBaseUrl> GetServiceBaseUrl(FhirSupport.FhirUri referenceUri)
  {
    if (referenceUri.UriPrimaryServiceRoot is null)
    {
      throw new NullReferenceException(nameof(referenceUri.UriPrimaryServiceRoot));
    }
    
    if (referenceUri.IsRelativeToServer)
    {
      return await serviceBaseUrlCache.GetRequiredPrimaryAsync();
    }
    
    ServiceBaseUrl? serviceBaseUrl = await serviceBaseUrlCache.GetByUrlAsync(referenceUri.UriPrimaryServiceRoot.FhirServiceBaseUrlFormattedString());
    if (serviceBaseUrl is not null)
    {
      return serviceBaseUrl;
    }
    
    return await serviceBaseUrlAddByUri.Add(new ServiceBaseUrl(
     serviceBaseUrlId: null, 
     url:referenceUri.UriPrimaryServiceRoot.FhirServiceBaseUrlFormattedString(), 
     isPrimary: false));
  }
}

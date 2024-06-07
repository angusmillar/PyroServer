using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.IndexSetters;

public class UriSetter : IUriSetter
{
  private FhirResourceTypeId ResourceType;
  private int SearchParameterId;
  private string? SearchParameterName;

  public IList<IndexUri> Set(ITypedElement typedElement, FhirResourceTypeId resourceType, int searchParameterId, string searchParameterName)
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

      return ProcessFhirDataType(fhirValueProvider.FhirValue);
    }

    if (typedElement.Value is null)
    {
      throw new NullReferenceException($"ITypedElement's Value found to be null for the SearchParameter entity with the database " +
                                       $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                       $"name of: {SearchParameterName}");
    }
    
    return ProcessPrimitiveDataType(typedElement.Value);
  }
  
  private IList<IndexUri> ProcessPrimitiveDataType(object obj)
  {
    switch (obj)
    {
      default:
        throw new FormatException($"Unknown Primitive DataType: {obj.GetType().Name} for the SearchParameter entity with the database " +
                                  $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                  $"name of: {SearchParameterName}");

    }
  }
  
  private IList<IndexUri> ProcessFhirDataType(Base fhirValue)
  {

    switch (fhirValue)
    {
      case FhirUri fhirUri:
        return SetUri(fhirUri);
      case FhirUrl fhirUrl:
        return SetUrl(fhirUrl);
      case Oid oid:
        return SetOid(oid);
      case Uuid uuid:
        return SetUuid(uuid);
      default:
        throw new FormatException($"Unknown FhirType: {fhirValue.GetType().Name} for the SearchParameter entity with the database " +
                                  $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                  $"name of: {SearchParameterName}");
    }
  }


  private IList<IndexUri> SetUuid(Uuid uuid)
  {
    return AddIndexUriToIndexListIfValid(uuid.Value);
  }

  private IList<IndexUri> SetOid(Oid oid)
  {
    return AddIndexUriToIndexListIfValid(oid.Value);
  }
  private IList<IndexUri> SetUri(FhirUri fhirUri)
  {
    return AddIndexUriToIndexListIfValid(fhirUri.Value);
  }

  private IList<IndexUri> SetUrl(FhirUrl fhirUrl)
  {
    return AddIndexUriToIndexListIfValid(fhirUrl.Value);
  }

  private IList<IndexUri> AddIndexUriToIndexListIfValid(string uriString)
  {
    if (string.IsNullOrWhiteSpace(uriString))
    {
      return Array.Empty<IndexUri>();
    }
    if (Uri.IsWellFormedUriString(uriString, UriKind.RelativeOrAbsolute))
    {
      return Array.Empty<IndexUri>();
    }

    return new List<IndexUri>() {
                                  new IndexUri(
                                    indexUriId: null,
                                    resourceStoreId: null,
                                    resourceStore: null,
                                    searchParameterStoreId: null,
                                    searchParameterStore: null,
                                    uri: uriString)
                                };
  }
}

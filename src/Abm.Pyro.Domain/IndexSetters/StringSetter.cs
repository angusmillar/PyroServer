using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Support;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.IndexSetters;

public class StringSetter : IStringSetter
{
  private FhirResourceTypeId ResourceType;
  private int SearchParameterId;
  private string? SearchParameterName;
  private const string ItemDelimiter = " ";

  public IList<IndexString> Set(ITypedElement typedElement, FhirResourceTypeId resourceType, int searchParameterId, string searchParameterName)
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

  private IList<IndexString> ProcessPrimitiveDataType(object obj)
  {
    switch (obj)
    {
      default:
        throw new FormatException($"Unknown Primitive DataType: {obj.GetType().Name} for the SearchParameter entity with the database " +
                                  $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                  $"name of: {SearchParameterName}");
    }
  }
  
  private IList<IndexString> ProcessFhirDataType(Base fhirValue)
  {
    switch (fhirValue)
    {
      case FhirString fhirString:
        return SetFhirString(fhirString);
      case Address address:
        return SetAddress(address);
      case HumanName humanName:
        return SetHumanName(humanName);
      case Markdown markdown:
        return SetMarkdown(markdown);
      case Annotation annotation:
        return SetAnnotation(annotation);
      case Base64Binary base64Binary:
        return Array.Empty<IndexString>(); //No good purpose to index base64 content as a search index
      default:
        throw new FormatException($"Unknown FhirType: {fhirValue.GetType().Name} for the SearchParameter entity with the database " +
                                  $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                  $"name of: {SearchParameterName}");
    }
  }

  private IList<IndexString> SetFhirString(FhirString fhirString)
  {
    return GetStringIndexIfNotNullOrWhiteSpace(fhirString.Value);
  }

  private IList<IndexString> SetAnnotation(Annotation annotation)
  {
    return GetStringIndexIfNotNullOrWhiteSpace(annotation.Text);
  }
  
  private IList<IndexString> SetMarkdown(Markdown markdown)
  {
    return GetStringIndexIfNotNullOrWhiteSpace(markdown.Value);
  }

  private  IList<IndexString> SetHumanName(HumanName humanName)
  {
    string fullName = string.Empty;
    if (!string.IsNullOrWhiteSpace(humanName.Family))
    {
      fullName = humanName.Family;
    }
  
    string givenNames = String.Join(StringSetter.ItemDelimiter, humanName.Given.Where(x => x is not null));
    if (!string.IsNullOrWhiteSpace(givenNames))
    {
      fullName = fullName + StringSetter.ItemDelimiter + givenNames;
    }
    
    if (string.IsNullOrEmpty(fullName))
    {
      return Array.Empty<IndexString>();
    }
  
    return new List<IndexString>() { SetIndexString(fullName) };
  }
  
  // private  IList<IndexString> SetHumanName(HumanName humanName)
  // {
  //   string fullName = string.Empty;
  //   foreach (var given in humanName.Given)
  //   {
  //     fullName += given + StringSetter.ItemDelimiter;
  //   }
  //
  //   if (!string.IsNullOrWhiteSpace(humanName.Family))
  //   {
  //     fullName += humanName.Family + StringSetter.ItemDelimiter;
  //   }
  //
  //   if (string.IsNullOrEmpty(fullName))
  //   {
  //     return Array.Empty<IndexString>();
  //   }
  //
  //   return new List<IndexString>() { SetIndexString(fullName) };
  // }
  private IList<IndexString> SetAddress(Address address)
  {
    string fullAddress = string.Empty;
    foreach (var line in address.Line)
    {
      fullAddress += line + StringSetter.ItemDelimiter;
    }
    if (!string.IsNullOrWhiteSpace(address.City))
    {
      fullAddress += address.City + ItemDelimiter;
    }
    if (!string.IsNullOrWhiteSpace(address.PostalCode))
    {
      fullAddress += address.PostalCode + ItemDelimiter;
    }
    if (!string.IsNullOrWhiteSpace(address.State))
    {
      fullAddress += address.State + ItemDelimiter;
    }
    if (!string.IsNullOrWhiteSpace(address.Country))
    {
      fullAddress += address.Country + ItemDelimiter;
    }
    if (string.IsNullOrEmpty(fullAddress))
    {
      return Array.Empty<IndexString>();
    }

    return new List<IndexString>() { SetIndexString(fullAddress) };
  }

  private IList<IndexString> GetStringIndexIfNotNullOrWhiteSpace(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return Array.Empty<IndexString>();
    }

    return new List<IndexString>() { SetIndexString(value) };
  }
  
  private IndexString SetIndexString(string value)
  {
    return new IndexString(
      indexStringId: null,
      resourceStoreId: null,
      resourceStore: null,
      searchParameterStoreId: SearchParameterId,
      searchParameterStore: null,
      value: LowerTrimRemoveDiacriticsAndTruncate(value));
  }
  
  private string LowerTrimRemoveDiacriticsAndTruncate(string item)
  {
    return StringSupport.ToLowerTrimRemoveDiacriticsTruncate(item, 450);
  }
}

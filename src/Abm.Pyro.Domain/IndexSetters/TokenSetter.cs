using System.Globalization;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Support;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;

namespace Abm.Pyro.Domain.IndexSetters;

public class TokenSetter : ITokenSetter
{
  private FhirResourceTypeId ResourceType;
  private int SearchParameterId;
  private string? SearchParameterName;

  public IList<IndexToken> Set(ITypedElement typedElement, FhirResourceTypeId resourceType, int searchParameterId, string searchParameterName)
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
  
  private IList<IndexToken> ProcessPrimitiveDataType(object obj)
  {
    switch (obj)
    {
      case bool boolean:
        return SetBoolean(boolean);
      default:
        throw new FormatException($"Unknown Primitive DataType: {obj.GetType().Name} for the SearchParameter entity with the database " +
                                  $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                  $"name of: {SearchParameterName}");

    }
  }
  
  private IList<IndexToken> ProcessFhirDataType(Base fhirValue)
  {
    switch (fhirValue)
    {
      case Code code:
        return SetCode(code);
      case CodeableConcept codeableConcept:
        return SetCodeableConcept(codeableConcept);
      case Coding coding:
        return SetCoding(coding);
      case ContactPoint contactPoint:
        return SetContactPoint(contactPoint);
      case FhirBoolean fhirBoolean:
        return SetFhirBoolean(fhirBoolean);
      case FhirDateTime fhirDateTime:
        return SetFhirDateTime(fhirDateTime);
      case FhirString fhirString:
        return SetFhirString(fhirString);
      case Id id:
        return SetId(id);
      case Identifier identifier:
        return SetIdentifier(identifier);
      case PositiveInt positiveInt:
        return SetPositiveInt(positiveInt);
      case Quantity quantity:
        return SetQuantity(quantity);
      case Hl7.Fhir.Model.Range range:
        return SetRange(range);
      case Location.PositionComponent positionComponent:
        return SePositionComponent(positionComponent);
      default:
      {
        if (fhirValue.TypeName == "code")
        {
          return SetCodeTypeT(fhirValue);
        }

        throw new FormatException($"Unknown FHIR DataType: {fhirValue.GetType().Name} for the SearchParameter entity with the database " +
                                  $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                  $"name of: {SearchParameterName}");
      }
    }
  }

  private IList<IndexToken> SetCodeTypeT(Base baseValue)
  {
    if (!baseValue.Any())
    {
      return Array.Empty<IndexToken>();
    }

    if (!baseValue.First().Key.Equals("value", StringComparison.OrdinalIgnoreCase))
    {
      return Array.Empty<IndexToken>();
    }

    if (baseValue.First().Value is string value)
    {
      if (string.IsNullOrWhiteSpace(value))
      {
        return Array.Empty<IndexToken>();
      }
      return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(null, value) };
    }

    return Array.Empty<IndexToken>();
  }

  private IList<IndexToken> SePositionComponent(Location.PositionComponent positionComponent)
  {
    if (positionComponent.Latitude != null && positionComponent.Latitude.HasValue && positionComponent.Longitude != null && positionComponent.Longitude.HasValue)
    {
      string code = string.Join(":", positionComponent.Latitude.Value, positionComponent.Longitude.Value);
      return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(null, code) };
    }

    if (positionComponent.Latitude != null && positionComponent.Latitude.HasValue)
    {
      string code = string.Join(":", positionComponent.Latitude.Value, string.Empty);
      return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(null, code) };
    }

    if (positionComponent.Longitude != null && positionComponent.Longitude.HasValue)
    {
      string code = string.Join(":", string.Empty, positionComponent.Longitude.Value);
      return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(null, code) };
    }

    return Array.Empty<IndexToken>();
  }

  private IList<IndexToken> SetRange(Hl7.Fhir.Model.Range range)
  {
    //There is no way to sensibly turn a Range into a Token type, so we just do nothing
    //and ignore setting the index. The reason this method is here is due to some search parameters
    //being a choice type where one of the choices is valid for token, like Boolean, yet others are 
    //not like Range as seen for the 'value' search parameter on the 'Group' resource .
    return Array.Empty<IndexToken>();
  }

  private IList<IndexToken> SetQuantity(Quantity quantity)
  {
    if (quantity.Value.HasValue && !string.IsNullOrWhiteSpace(quantity.Unit))
    {
      return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(quantity.Unit, Convert.ToString(quantity.Value.Value, CultureInfo.CurrentCulture)) };
    }

    if (quantity.Value.HasValue)
    {
      return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(null, Convert.ToString(quantity.Value.Value, CultureInfo.CurrentCulture)) };
    }

    if (!string.IsNullOrWhiteSpace(quantity.Unit))
    {
      return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(quantity.Unit, null) };
    }

    return Array.Empty<IndexToken>();
  }

  private IList<IndexToken> SetPositiveInt(PositiveInt positiveInt)
  {
    if (!positiveInt.Value.HasValue)
    {
      return Array.Empty<IndexToken>();
    }

    string? asString = Convert.ToString(positiveInt.Value);
    if (string.IsNullOrWhiteSpace(asString))
    {
      return Array.Empty<IndexToken>();
    }

    return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(null, asString) };
  }

  private IList<IndexToken> SetIdentifier(Identifier identifier)
  {
    if (!string.IsNullOrWhiteSpace(identifier.Value) && !string.IsNullOrWhiteSpace(identifier.System))
    {
      return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(identifier.System, identifier.Value) };
    }

    if (!string.IsNullOrWhiteSpace(identifier.Value))
    {
      return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(null, identifier.Value) };
    }

    if (!string.IsNullOrWhiteSpace(identifier.System))
    {

      return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(identifier.System, null) };
    }
    
    return Array.Empty<IndexToken>();
  }

  private IList<IndexToken> SetId(Id id)
  {
    if (string.IsNullOrWhiteSpace(id.Value))
    {
      return Array.Empty<IndexToken>();
    }

    return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(null, id.Value) };
  }

  private IList<IndexToken> SetFhirString(FhirString fhirString)
  {
    if (string.IsNullOrWhiteSpace(fhirString.Value))
    {
      return Array.Empty<IndexToken>();
    }

    return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(null, fhirString.Value) };
  }

  private IList<IndexToken> SetFhirDateTime(FhirDateTime fhirDateTime)
  {
    if (string.IsNullOrWhiteSpace(fhirDateTime.Value))
    {
      return Array.Empty<IndexToken>();
    }

    if (!FhirDateTime.IsValidValue(fhirDateTime.Value))
    {
      return Array.Empty<IndexToken>();
    }

    return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(null, fhirDateTime.Value) };
  }

  private IList<IndexToken> SetBoolean(bool boolean)
  {
    return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(null, boolean.ToString()) };
  }

  private IList<IndexToken> SetFhirBoolean(FhirBoolean fhirBoolean)
  {
    if (!fhirBoolean.Value.HasValue)
    {
      return Array.Empty<IndexToken>();
    }
    return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(null, fhirBoolean.Value.ToString()) };
  }

  private IList<IndexToken> SetContactPoint(ContactPoint contactPoint)
  {
    if (!string.IsNullOrWhiteSpace(contactPoint.Value) && (contactPoint.System != null))
    {
      return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(contactPoint.System.GetLiteral(), contactPoint.Value) };
    }

    if (!string.IsNullOrWhiteSpace(contactPoint.Value))
    {
      return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(null, contactPoint.Value) };
    }

    if (contactPoint.System != null)
    {
      return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(contactPoint.System.GetLiteral(), null) };
    }

    return Array.Empty<IndexToken>();
  }

  private IList<IndexToken> SetCodeableConcept(CodeableConcept codeableConcept)
  {
    if (codeableConcept.Coding.Count == 0)
    {
      if (string.IsNullOrWhiteSpace(codeableConcept.Text))
      {
        return Array.Empty<IndexToken>();
      }
      return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(null, codeableConcept.Text) };
    }

    var result = new List<IndexToken>();
    foreach (Coding code in codeableConcept.Coding)
    {
      result.AddRange(SetCoding(code));
    }
    return result;
  }

  private IList<IndexToken> SetCoding(Coding coding)
  {
    if (!string.IsNullOrWhiteSpace(coding.Code) && !string.IsNullOrWhiteSpace(coding.System))
    {
      return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(coding.System, coding.Code) };
    }

    if (!string.IsNullOrWhiteSpace(coding.Code))
    {
      return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(null, coding.Code) };
    }

    if (!string.IsNullOrWhiteSpace(coding.System))
    {
      return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(coding.System, null) };
    }

    return Array.Empty<IndexToken>();
  }

  private IList<IndexToken> SetCode(Code code)
  {
    if (string.IsNullOrWhiteSpace(code.Value))
    {
      return Array.Empty<IndexToken>();
    }
    return new List<IndexToken>() { SetTokenIndexToLowerCaseTrim(null, code.Value) };
  }

  private IndexToken SetTokenIndexToLowerCaseTrim(string? system, string? code)
  {
    if (!string.IsNullOrWhiteSpace(system))
    {
      system = StringSupport.ToLowerFast(system.Trim());
    }

    if (!string.IsNullOrWhiteSpace(code))
    {
      code = StringSupport.ToLowerFast(code.Trim());
    }

    return new IndexToken(
      indexTokenId: null,
      resourceStoreId: null,
      resourceStore: null,
      searchParameterStoreId: SearchParameterId,
      searchParameterStore: null,
      code: code,
      system: system);
  }
}

using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;

namespace Abm.Pyro.Domain.IndexSetters;

public class QuantitySetter(IQuantityComparatorMap quantityComparatorMap) : IQuantitySetter
{
  private FhirResourceTypeId ResourceType;
  private int SearchParameterId;
  private string? SearchParameterName;

  public IList<IndexQuantity> Set(ITypedElement typedElement, FhirResourceTypeId resourceType, int searchParameterId, string searchParameterName)
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

  private IList<IndexQuantity> ProcessPrimitiveDataType(object obj)
  {
    switch (obj)
    {
      default:
        throw new FormatException($"Unknown Primitive DataType: {obj.GetType().Name} for the SearchParameter entity with the database " +
                                  $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                  $"name of: {SearchParameterName}");

    }
  }
  
  private IList<IndexQuantity> ProcessFhirDataType(Base fhirValue)
  {

    switch (fhirValue)
    {
      case Money money:
        return SetMoney(money);
      case Quantity quantity:
        return SetQuantity(quantity);
      case Location.PositionComponent positionComponent:
        return SetPositionComponent(positionComponent);
      case Hl7.Fhir.Model.Range range:
        return SetRange(range);
      default:
        throw new FormatException($"Unknown FhirType: {fhirValue.GetType().Name} for the SearchParameter entity with the database " +
                                  $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                  $"name of: {SearchParameterName}");
    }
  }
  
  
  private IList<IndexQuantity> SetRange(Hl7.Fhir.Model.Range range)
  {
    //If either value is missing then their is no range as the Range data type uses SimpleQuantity 
    //which has no Comparator property. Therefore there is no such thing as >10 or <100, their must be to values
    // for examples 10 - 100. 
    if (!range.High.Value.HasValue && !range.Low.Value.HasValue)
    {
      return Array.Empty<IndexQuantity>();
    }

    QuantityComparator? comparatorLow = null;
    if (range.Low.Comparator.HasValue)
    {
      comparatorLow = quantityComparatorMap.Map(range.Low.Comparator.Value);
    }

    QuantityComparator? comparatorHigh = null;
    if (range.High.Comparator.HasValue)
    {
      comparatorHigh = quantityComparatorMap.Map(range.High.Comparator.Value);
    }

    var resourceIndex = SetIndexQuantityRange(range.Low.Value,
                                              range.Low.Code,
                                              range.Low.System,
                                              comparatorLow,
                                              range.Low.Unit,
                                              range.High.Value,
                                              string.IsNullOrWhiteSpace(range.High.Code) ? null : range.High.Code,
                                              string.IsNullOrWhiteSpace(range.High.System) ? null : range.High.System,
                                              comparatorHigh,
                                              string.IsNullOrWhiteSpace(range.High.Unit) ? null : range.High.Unit);

    return new List<IndexQuantity>() { resourceIndex };

  }
  private IList<IndexQuantity> SetPositionComponent(Location.PositionComponent positionComponent)
  {
    //todo:
    //The only Quantity for Location.PositionComponent is in the Location resource and it's use is a little odd.
    //You never actual store a 'near-distance' search parameter as an index but rather it is used in conjunction with the 
    //'near' search parameter. 
    //for instance the search would be like this:
    //GET [base]/Location?near=-83.694810:42.256500&near-distance=11.20||km...
    //Where we need to work out the distance say in km between 'near' [latitude]:[longitude] we have stored in the db index and the [latitude]:[longitude] given in the search url's 'near'.
    //If that distance is less then or equal to the  'near-distance' given in the search Url (11.20km here) then return the resource.   
    //Update: Talked to Brian Pos and I can see I do need to store this as it's own index. SQL has a geography datatype which needs to be used
    //See ref: https://docs.microsoft.com/en-us/sql/t-sql/spatial-geography/spatial-types-geography
    // I also have some of Brians code as an example in NOTES, search for 'Brian's FHIR position longitude latitude code' in FHIR notebook
    //I think this will have to be a special case, maybe not a noraml Pyro FHIR token index but another, or maybe add it to the Token index yet it will be null 99% of time. 
    //More thinking required. At present the server never indexes this so the search never finds it. Not greate!
    return Array.Empty<IndexQuantity>();
  }
  private IList<IndexQuantity> SetQuantity(Quantity quantity)
  {
    QuantityComparator? comparator = null;
    if (quantity.Comparator.HasValue)
    {
      comparator = quantityComparatorMap.Map(quantity.Comparator.Value);
    }

    var resourceIndex = SetIndexQuantity(quantity.Value, quantity.Code, quantity.System, comparator, quantity.Unit);

    return new List<IndexQuantity>() { resourceIndex };
  }

  private IList<IndexQuantity> SetMoney(Money money)
  {
    if (!money.Currency.HasValue)
    {
      return Array.Empty<IndexQuantity>();
    }

    string code = money.Currency.Value.GetLiteral();
    string system = "urn:iso:std:iso:4217";

    var resourceIndex = SetIndexQuantity(money.Value, code, system, null, null);

    return new List<IndexQuantity>() { resourceIndex };
  }
  private IndexQuantity SetIndexQuantity(decimal? quantity, string? code, string? system, QuantityComparator? comparator, string? units)
  {
    var resourceIndex = new IndexQuantity(
      indexQuantityId: null,
      resourceStoreId: null,
      resourceStore: null,
      searchParameterStoreId: SearchParameterId,
      searchParameterStore: null,
      comparator: comparator,
      quantity: quantity,
      code: code,
      system: system,
      unit: units,
      comparatorHigh: null,
      quantityHigh: null,
      codeHigh: null,
      systemHigh: null,
      unitHigh: null
    );
    return resourceIndex;
  }

  private IndexQuantity SetIndexQuantityRange(decimal? quantityLow, string? codeLow, string? systemLow, QuantityComparator? comparatorLow, string? unitsLow,
                                              decimal? quantityHigh, string? codeHigh, string? systemHigh, QuantityComparator? comparatorHigh, string? unitsHigh)
  {
    var resourceIndex = new IndexQuantity(
      indexQuantityId: null,
      resourceStoreId: null,
      resourceStore: null,
      searchParameterStoreId: SearchParameterId,
      searchParameterStore: null,
      comparator: comparatorLow,
      quantity: quantityLow,
      code: codeLow,
      system: systemLow,
      unit: unitsLow,
      comparatorHigh: comparatorHigh,
      quantityHigh: quantityHigh,
      codeHigh: codeHigh,
      systemHigh: systemHigh,
      unitHigh: unitsHigh
    );
    return resourceIndex;
  }
}

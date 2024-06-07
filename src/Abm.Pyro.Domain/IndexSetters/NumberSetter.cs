using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.IndexSetters;

public class NumberSetter : INumberSetter
{
  private FhirResourceTypeId ResourceType;
  private int SearchParameterId;
  private string? SearchParameterName;
  private readonly QuantityComparatorMap QuantityComparatorMap = new();

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
      case Integer integer:
        return SetInteger(integer);
      case PositiveInt positiveInt:
        return SetPositiveInt(positiveInt);
      case Duration duration:
        return SetDuration(duration);
      case FhirDecimal fhirDecimal:
        return SetFhirDecimal(fhirDecimal);
      case Hl7.Fhir.Model.Range range:
        return SetRange(range);
      default:
        throw new FormatException($"Unknown FhirType: {fhirValue.GetType().Name} for the SearchParameter entity with the database " +
                                  $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                  $"name of: {SearchParameterName}");
    }
  }

  private IList<IndexQuantity> SetFhirDecimal(FhirDecimal fhirDecimal)
  {
    if (!fhirDecimal.Value.HasValue)
    {
      return Array.Empty<IndexQuantity>();
    }
    return AddIndexQuantityToIndexList(fhirDecimal.Value, null);
  }

  private IList<IndexQuantity> SetRange(Hl7.Fhir.Model.Range range)
  {
    var result = new List<IndexQuantity>();
    if (range.Low?.Value != null)
    {
      result.AddRange(AddIndexQuantityToIndexList(range.Low.Value, QuantityComparator.GreaterOrEqual));
    }
    if (range.High?.Value != null)
    {
      result.AddRange(AddIndexQuantityToIndexList(range.High.Value, QuantityComparator.LessOrEqual));
    }
    return result;
  }

  private IList<IndexQuantity> SetDuration(Duration duration)
  {
    if (!duration.Value.HasValue)
    {
      return Array.Empty<IndexQuantity>();
    }

    QuantityComparator? comparator = null;

    if (duration.Comparator.HasValue)
      comparator = QuantityComparatorMap.Map(duration.Comparator.Value);

    return AddIndexQuantityToIndexList((decimal)duration.Value, comparator);

  }

  private IList<IndexQuantity> SetPositiveInt(PositiveInt positiveInt)
  {
    if (!positiveInt.Value.HasValue)
    {
      return Array.Empty<IndexQuantity>();
    }
    if (positiveInt.Value < 0)
      throw new FormatException($"PositiveInt must be a positive value, value was : {positiveInt.Value.ToString()}");

    return AddIndexQuantityToIndexList(Convert.ToInt32(positiveInt.Value), null);
  }

  private IList<IndexQuantity> SetInteger(Integer integer)
  {
    if (!integer.Value.HasValue)
    {
      return Array.Empty<IndexQuantity>();
    }
    return AddIndexQuantityToIndexList(Convert.ToInt32(integer.Value), null);
  }

  private IList<IndexQuantity> AddIndexQuantityToIndexList(decimal? quantity, QuantityComparator? comparator)
  {
    if (!quantity.HasValue)
    {
      return Array.Empty<IndexQuantity>();
    }
    return new List<IndexQuantity>() {
                                       new IndexQuantity(
                                         indexQuantityId: null,
                                         resourceStoreId: null,
                                         resourceStore: null,
                                         searchParameterStoreId: SearchParameterId,
                                         searchParameterStore: null,
                                         comparator: comparator,
                                         quantity: quantity,
                                         code: null,
                                         system: null,
                                         unit: null,
                                         comparatorHigh: null,
                                         quantityHigh: null,
                                         codeHigh: null,
                                         systemHigh: null,
                                         unitHigh: null)
                                     };

  }
}

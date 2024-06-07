using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.Enums;

public class QuantityComparatorMap : MapBase<QuantityComparator, Quantity.QuantityComparator>, IQuantityComparatorMap
{
  private readonly Dictionary<QuantityComparator, Quantity.QuantityComparator> ForwardMapDictionary;
  private readonly Dictionary<Quantity.QuantityComparator, QuantityComparator> ReverseMapDictionary;
  protected override Dictionary<QuantityComparator, Quantity.QuantityComparator> ForwardMap { get { return ForwardMapDictionary; } }
  protected override Dictionary<Quantity.QuantityComparator, QuantityComparator> ReverseMap { get { return ReverseMapDictionary; } }
  
  public QuantityComparatorMap()
  {
    ForwardMapDictionary = new Dictionary<QuantityComparator, Quantity.QuantityComparator>();
    ForwardMapDictionary.Add(QuantityComparator.GreaterOrEqual, Quantity.QuantityComparator.GreaterOrEqual);
    ForwardMapDictionary.Add(QuantityComparator.GreaterThan, Quantity.QuantityComparator.GreaterThan);
    ForwardMapDictionary.Add(QuantityComparator.LessOrEqual, Quantity.QuantityComparator.LessOrEqual);
    ForwardMapDictionary.Add(QuantityComparator.LessThan, Quantity.QuantityComparator.LessThan);

    //Auto Generate the reverse map
    ReverseMapDictionary = ForwardMapDictionary.ToDictionary((i) => i.Value, (i) => i.Key);

  }
}

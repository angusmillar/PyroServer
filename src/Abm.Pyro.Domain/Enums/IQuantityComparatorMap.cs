using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.Enums;

public interface IQuantityComparatorMap
{
  Quantity.QuantityComparator Map(QuantityComparator value);
  QuantityComparator Map(Quantity.QuantityComparator value);
}

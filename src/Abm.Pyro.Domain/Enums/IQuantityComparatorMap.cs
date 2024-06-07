using Hl7.Fhir.Model;
using Abm.Pyro.Domain.Enums;
namespace Abm.Pyro.Domain.Enums;

public interface IQuantityComparatorMap
{
  Quantity.QuantityComparator Map(QuantityComparator value);
  QuantityComparator Map(Quantity.QuantityComparator value);
}

using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.IndexSetters;

public interface IQuantitySetter
{
  IList<IndexQuantity> Set(ITypedElement typedElement, FhirResourceTypeId resourceType, int searchParameterId, string searchParameterName);
}

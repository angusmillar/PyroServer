using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Hl7.Fhir.ElementModel;

namespace Abm.Pyro.Domain.IndexSetters;

public interface ITokenSetter
{
  IList<IndexToken> Set(ITypedElement typedElement, FhirResourceTypeId resourceType, int searchParameterId, string searchParameterName);
}

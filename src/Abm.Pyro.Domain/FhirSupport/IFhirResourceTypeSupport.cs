using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Domain.FhirSupport;

public interface IFhirResourceTypeSupport
{
  FhirResourceTypeId GetRequiredFhirResourceType(string resourceName);
  FhirResourceTypeId? TryGetResourceType(string resourceName);
  
}

public interface IFhirResourceNameSupport
{
  bool IsResourceTypeString(string resourceName);
}

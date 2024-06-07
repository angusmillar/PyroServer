using System.Diagnostics.CodeAnalysis;

namespace Abm.Pyro.Domain.FhirSupport;

public interface IFhirUriFactory
{
  bool TryParse(string requestUri, [NotNullWhen(true)] out FhirUri? fhirUri, out string errorMessage);
}

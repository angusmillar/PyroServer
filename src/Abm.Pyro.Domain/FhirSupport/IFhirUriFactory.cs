using System.Diagnostics.CodeAnalysis;

namespace Abm.Pyro.Domain.FhirSupport;

public interface IFhirUriFactory
{
  Task<(bool Success, FhirUri? fhirUri, string errorMessage)> TryParse2(
    string requestUri);
  
  bool TryParse(
    string requestUri, 
    [NotNullWhen(true)] out FhirUri? fhirUri, 
    out string errorMessage);
}

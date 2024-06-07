using Abm.Pyro.Domain.FhirSupport;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public class SearchQueryReferenceValue(bool isMissing, FhirUri? fhirUri) : SearchQueryValueBase(isMissing)
{
  public FhirUri? FhirUri { get; set; } = fhirUri;
}

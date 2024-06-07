using Abm.Pyro.Domain.Query;
using Hl7.Fhir.Model;
using Abm.Pyro.Domain.SearchQuery;
namespace Abm.Pyro.Domain.FhirSupport;

public interface IFhirBundleCreationSupport
{
  Task<Bundle> CreateBundle(ResourceStoreSearchOutcome resourceStoreSearchOutcome, Bundle.BundleType bundleType, string requestSchema);
}

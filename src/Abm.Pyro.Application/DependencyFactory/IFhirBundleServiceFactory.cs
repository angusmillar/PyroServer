using Abm.Pyro.Application.FhirBundleService;
using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Application.DependencyFactory;

public interface IFhirBundleServiceFactory
{
    public IFhirBundleService Resolve(BundleType bundleType);
}
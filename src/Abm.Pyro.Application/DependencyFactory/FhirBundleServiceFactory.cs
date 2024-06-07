using Abm.Pyro.Application.FhirBundleService;
using Microsoft.Extensions.DependencyInjection;
using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Application.DependencyFactory;

public class FhirBundleServiceFactory(IServiceProvider serviceProvider) : IFhirBundleServiceFactory
{
    public IFhirBundleService Resolve(BundleType bundleType)
    {
        switch (bundleType)
        {
            case BundleType.Transaction:
                return (IFhirBundleService)serviceProvider.GetRequiredService(typeof(FhirTransactionService));
            case BundleType.Batch:
                return (IFhirBundleService)serviceProvider.GetRequiredService(typeof(FhirBatchService));
            default:
                throw new ArgumentOutOfRangeException(nameof(bundleType), bundleType, null);
        } 
    }
}
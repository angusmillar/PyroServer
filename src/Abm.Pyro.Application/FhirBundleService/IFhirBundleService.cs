using Abm.Pyro.Domain.FhirBundleService;
using Abm.Pyro.Domain.FhirResponse;

namespace Abm.Pyro.Application.FhirBundleService;

public interface IFhirBundleService
{
    Task<FhirResourceResponse> Process(FhirBundleRequest request, CancellationToken cancellationToken);
}
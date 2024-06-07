using Abm.Pyro.Application.FhirResponse;

namespace Abm.Pyro.Application.FhirBundleService;

public interface IFhirBundleService
{
    Task<FhirResourceResponse> Process(FhirBundleRequest request, CancellationToken cancellationToken);
}
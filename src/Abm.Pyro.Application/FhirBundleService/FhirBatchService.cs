using Abm.Pyro.Domain.FhirBundleService;
using Abm.Pyro.Domain.FhirResponse;

namespace Abm.Pyro.Application.FhirBundleService;

public class FhirBatchService : IFhirBundleService
{
    public async Task<FhirResourceResponse> Process(FhirBundleRequest request,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        throw new NotImplementedException();
    }
}
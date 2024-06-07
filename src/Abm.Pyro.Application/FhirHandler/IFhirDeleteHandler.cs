using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Domain.Projection;

namespace Abm.Pyro.Application.FhirHandler;

public interface IFhirDeleteHandler
{
    Task<FhirOptionalResourceResponse> Handle(string resourceName, string resourceId, CancellationToken cancellationToken, ResourceStoreUpdateProjection? previousResourceStore = null);
}
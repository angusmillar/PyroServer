using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.Projection;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Application.FhirHandler;

public interface IFhirPatchHandler
{
    Task<FhirOptionalResourceResponse> Handle(
        string tenant,
        string requestId,
        string resourceId,
        string resourceName,
        Parameters patchParameters,
        Dictionary<string, StringValues> headers,
        CancellationToken cancellationToken,
        ResourceStoreUpdateProjection? previousResourceStore = null);
}

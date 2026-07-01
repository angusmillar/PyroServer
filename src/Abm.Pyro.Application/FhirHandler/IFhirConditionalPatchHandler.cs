using Abm.Pyro.Domain.FhirResponse;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Application.FhirHandler;

public interface IFhirConditionalPatchHandler
{
    Task<FhirOptionalResourceResponse> Handle(
        string tenant,
        string requestId,
        string resourceName,
        string query,
        Parameters patchParameters,
        Dictionary<string, StringValues> headers,
        CancellationToken cancellationToken);
}

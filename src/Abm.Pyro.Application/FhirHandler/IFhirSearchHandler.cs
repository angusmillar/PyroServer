using Abm.Pyro.Application.FhirResponse;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Application.FhirHandler;

public interface IFhirSearchHandler
{
    Task<FhirResourceResponse> Handle(string tenant, string requestId, string resourceName, string query, Dictionary<string, StringValues> headers, CancellationToken cancellationToken);
}
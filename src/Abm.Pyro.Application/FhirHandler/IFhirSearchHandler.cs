using Abm.Pyro.Application.FhirResponse;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Application.FhirHandler;

public interface IFhirSearchHandler
{
    Task<FhirResourceResponse> Handle(string tenant, string ResourceName, string query, Dictionary<string, StringValues> headers, CancellationToken cancellationToken);
}
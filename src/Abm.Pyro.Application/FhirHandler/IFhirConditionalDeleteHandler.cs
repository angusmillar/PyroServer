using Abm.Pyro.Application.FhirResponse;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Application.FhirHandler;

public interface IFhirConditionalDeleteHandler
{
    Task<FhirOptionalResourceResponse> Handle(string tenant, string resourceName, string query, Dictionary<string, StringValues> headers, CancellationToken cancellationToken);
}
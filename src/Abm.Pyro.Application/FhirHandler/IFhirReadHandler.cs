using Abm.Pyro.Application.FhirResponse;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Application.FhirHandler;

public interface IFhirReadHandler
{
    Task<FhirOptionalResourceResponse> Handle(string resourceName, string resourceId, CancellationToken cancellationToken, Dictionary<string, StringValues>? headers = null);
}
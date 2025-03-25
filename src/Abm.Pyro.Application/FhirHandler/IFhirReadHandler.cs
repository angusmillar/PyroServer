using Abm.Pyro.Domain.FhirResponse;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Application.FhirHandler;

public interface IFhirReadHandler
{
    Task<FhirOptionalResourceResponse> Handle(
        string tenant,
        string requestId,
        string resourceName,
        string resourceId,
        CancellationToken cancellationToken,
        Dictionary<string, StringValues>? headers = null);
}
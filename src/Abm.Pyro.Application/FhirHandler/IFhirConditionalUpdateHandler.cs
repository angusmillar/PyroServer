using Abm.Pyro.Application.FhirResponse;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Application.FhirHandler;

public interface IFhirConditionalUpdateHandler
{
    Task<FhirOptionalResourceResponse> Handle(string tenant, string requestId, string query, Resource resource, Dictionary<string, StringValues> headers, CancellationToken cancellationToken);
}
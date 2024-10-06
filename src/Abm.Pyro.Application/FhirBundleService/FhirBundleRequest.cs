using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Application.FhirBundleService;

public record FhirBundleRequest(
    string RequestSchema,
    string Tenant,
    string RequestId,
    string RequestPath,
    string? QueryString,
    Dictionary<string, StringValues> Headers,
    Bundle Bundle,
    DateTimeOffset TimeStamp);
    
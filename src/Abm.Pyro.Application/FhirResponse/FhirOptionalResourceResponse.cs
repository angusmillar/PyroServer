using System.Net;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Application.FhirResponse;

public record FhirOptionalResourceResponse(Resource? Resource, HttpStatusCode HttpStatusCode, Dictionary<string, StringValues> Headers, ResourceOutcomeInfo? ResourceOutcomeInfo = null)
    : FhirResponse(HttpStatusCode, Headers, ResourceOutcomeInfo);

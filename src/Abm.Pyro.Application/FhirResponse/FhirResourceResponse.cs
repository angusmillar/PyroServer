using System.Net;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Application.FhirResponse;

public record FhirResourceResponse(Resource Resource, HttpStatusCode HttpStatusCode, Dictionary<string, StringValues> Headers, ResourceOutcomeInfo? ResourceOutcomeInfo = null, bool CanCommitTransaction = true)
  : FhirResponse(HttpStatusCode, Headers, ResourceOutcomeInfo, CanCommitTransaction);
  
using System.Net;
using Abm.Pyro.Domain.Notification;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Domain.FhirResponse;

public record FhirResourceResponse(
    Resource Resource,
    HttpStatusCode HttpStatusCode,
    Dictionary<string, StringValues> Headers,
    IRepositoryEventCollector RepositoryEventCollector,
    ResourceOutcomeInfo? ResourceOutcomeInfo = null,
    bool CanCommitTransaction = true)
  : FhirResponse(
      HttpStatusCode, 
      Headers, 
      RepositoryEventCollector, 
      ResourceOutcomeInfo, CanCommitTransaction);
  
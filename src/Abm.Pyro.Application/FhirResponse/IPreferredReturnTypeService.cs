using System.Collections.Concurrent;
using System.Net;
using Abm.Pyro.Application.Notification;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Application.FhirResponse;

public interface IPreferredReturnTypeService
{
    FhirOptionalResourceResponse GetResponse(
        HttpStatusCode httpStatusCode,
        Resource resource,
        int versionId,
        Dictionary<string, StringValues> requestHeaders,
        Dictionary<string, StringValues> responseHeaders,
        IRepositoryEventCollector repositoryEventQueue);
}
using System.Net;
using Abm.Pyro.Domain.Notification;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Domain.FhirResponse;

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
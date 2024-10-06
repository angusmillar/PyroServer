using System.Collections.Concurrent;
using System.Net;
using Abm.Pyro.Application.Notification;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;

namespace Abm.Pyro.Application.FhirResponse;

public class PreferredReturnTypeService(
    IFhirRequestHttpHeaderSupport fhirRequestHttpHeaderSupport,
    IOperationOutcomeSupport operationOutcomeSupport)
    : IPreferredReturnTypeService
{
    public FhirOptionalResourceResponse GetResponse(
        HttpStatusCode httpStatusCode, 
        Resource resource,
        int versionId,
        Dictionary<string, StringValues> requestHeaders,
        Dictionary<string, StringValues> responseHeaders,
        IRepositoryEventCollector repositoryEventQueue)
    {
        PreferReturnType preferReturnType = fhirRequestHttpHeaderSupport.GetPreferReturn(requestHeaders);
        switch (preferReturnType)
        {
            case PreferReturnType.Minimal:
                return new FhirOptionalResourceResponse(
                    Resource: null, 
                    HttpStatusCode: httpStatusCode, 
                    Headers: responseHeaders,
                    RepositoryEventCollector: repositoryEventQueue, 
                    ResourceOutcomeInfo: new ResourceOutcomeInfo(resourceId: resource.Id, versionId: versionId));
            case PreferReturnType.Representation:
                return new FhirOptionalResourceResponse(
                    Resource: resource, 
                    HttpStatusCode: httpStatusCode, 
                    Headers: responseHeaders,
                    RepositoryEventCollector: repositoryEventQueue, 
                    ResourceOutcomeInfo: new ResourceOutcomeInfo(resourceId: resource.Id, versionId: versionId));
            case PreferReturnType.OperationOutcome:
                return new FhirOptionalResourceResponse(Resource: operationOutcomeSupport.GetInformation(
                        new[]
                        {
                            $"HttpStatusCode: {((int)httpStatusCode).ToString()} ({httpStatusCode.ToString()})",
                            $"ResourceType: {resource.TypeName}",
                            $"ResourceId: {resource.Id}",
                            $"VersionId: {versionId.ToString()}",
                        }), 
                    HttpStatusCode: HttpStatusCode.Created, 
                    Headers: responseHeaders,
                    RepositoryEventCollector: repositoryEventQueue, 
                    ResourceOutcomeInfo: new ResourceOutcomeInfo(resourceId: resource.Id, versionId: versionId));
            default:
                throw new ArgumentOutOfRangeException(nameof(preferReturnType));
        }
    }
}
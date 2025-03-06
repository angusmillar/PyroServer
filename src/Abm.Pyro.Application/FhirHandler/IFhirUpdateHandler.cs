using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirResponse;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Projection;

namespace Abm.Pyro.Application.FhirHandler;

public interface IFhirUpdateHandler
{
    Task<FhirOptionalResourceResponse> Handle(
        string tenant,
        string requestId,
        string resourceId, 
        Resource resource, 
        Dictionary<string, StringValues> headers, 
        CancellationToken cancellationToken, 
        ResourceStoreUpdateProjection? previousResourceStore = null);
    
    Task<FhirOptionalResourceResponse> HandleSystemSubscriptionUpdate(
        SystemSubscriptionUpdateRequest systemSubscriptionUpdateRequest, 
        CancellationToken cancellationToken);
}
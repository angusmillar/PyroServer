using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirResponse;

namespace Abm.Pyro.Domain.FhirOperation;

public interface IFhirInstanceOperationService : IFhirOperationService
{
    Task<FhirResourceResponse> Handle(FhirInstanceLevelOperationRequest request);
}
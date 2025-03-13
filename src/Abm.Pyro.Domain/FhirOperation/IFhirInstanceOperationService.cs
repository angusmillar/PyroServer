using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirResponse;

namespace Abm.Pyro.Domain.FhirOperation;

public interface IFhirInstanceOperationService : IFhirOperationService
{
    FhirResourceResponse Handle(FhirInstanceLevelOperationRequest request);
}
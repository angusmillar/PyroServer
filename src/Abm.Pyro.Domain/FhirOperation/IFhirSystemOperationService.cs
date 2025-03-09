using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirResponse;

namespace Abm.Pyro.Domain.FhirOperation;

public interface IFhirSystemOperationService : IFhirOperationService
{
    Task<FhirResourceResponse>  Handle(FhirSystemLevelOperationRequest request);
}
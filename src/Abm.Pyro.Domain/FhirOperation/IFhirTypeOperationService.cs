using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirResponse;

namespace Abm.Pyro.Domain.FhirOperation;

public interface IFhirTypeOperationService : IFhirOperationService
{
    Task<FhirResourceResponse> Handle(FhirTypeLevelOperationRequest request);
}
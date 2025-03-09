using Abm.Pyro.Domain.FhirOperation;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirResponse;

namespace Abm.Pyro.Application.FhirValidateService;

public interface IFhirValidateOperationService : IFhirSystemOperationService, IFhirTypeOperationService, IFhirInstanceOperationService
{
    new Task<FhirResourceResponse> Handle(FhirSystemLevelOperationRequest request);
    new string Handle(string test);
}
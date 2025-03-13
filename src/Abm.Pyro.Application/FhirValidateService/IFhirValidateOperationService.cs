using Abm.Pyro.Domain.FhirOperation;

namespace Abm.Pyro.Application.FhirValidateService;

public interface IFhirValidateOperationService :
    IFhirTypeOperationService,
    IFhirInstanceOperationService;


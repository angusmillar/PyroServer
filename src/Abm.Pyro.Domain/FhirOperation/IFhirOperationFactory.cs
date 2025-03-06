using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Domain.FhirOperation;

public interface IFhirOperationFactory
{
    public IFhirOperationService? Get(
        FhirOperationLevel fhirOperationLevel,
        string operationName);
}
namespace Abm.Pyro.Domain.FhirOperation;

public interface IFhirTypeOperationService : IFhirOperationService
{
    string Handle(string test);
}
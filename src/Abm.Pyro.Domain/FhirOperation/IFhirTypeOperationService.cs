namespace Abm.Pyro.Domain.FhirOperation;

public interface IFhirTypeOperationService : IFhirOperationService
{
    public string Handle(string test);
}
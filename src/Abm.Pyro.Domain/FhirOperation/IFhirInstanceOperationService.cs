namespace Abm.Pyro.Domain.FhirOperation;

public interface IFhirInstanceOperationService : IFhirOperationService
{
    public string Handle(string test);
}
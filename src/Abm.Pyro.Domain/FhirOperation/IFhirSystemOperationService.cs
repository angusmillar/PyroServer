namespace Abm.Pyro.Domain.FhirOperation;

public interface IFhirSystemOperationService : IFhirOperationService
{
    public string Handle(string request);
}
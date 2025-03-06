using Abm.Pyro.Domain.FhirOperation;

namespace Abm.Pyro.Application.FhirValidateService;

public class FhirValidateOperationService : IFhirSystemOperationService, IFhirTypeOperationService, IFhirInstanceOperationService
{
    public const string OperationName = "validate";
    
    public string Handle(string request)
    {
        
        //Terminology Server for Sparked 
        //https://tx.dev.hl7.org.au/fhir

        throw new NotImplementedException();
    }
}
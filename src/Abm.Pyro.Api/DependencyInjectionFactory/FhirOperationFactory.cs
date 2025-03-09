using Abm.Pyro.Application.FhirValidateService;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirOperation;

namespace Abm.Pyro.Api.DependencyInjectionFactory;

public class FhirOperationFactory(IServiceProvider serviceProvider) : IFhirOperationFactory
{
    public IFhirOperationService? Get(FhirOperationLevel fhirOperationLevel, string operationName)
    {
        switch (fhirOperationLevel)
        {
            case FhirOperationLevel.System:
                return GetSystemLevelOperation(operationName);
            case FhirOperationLevel.Type:
                return GetTypeLevelOperation(operationName);
            case FhirOperationLevel.Instance:
                return GetInstanceLevelOperation(operationName);
            default:
                return null;
        }
    }

    private IFhirSystemOperationService? GetSystemLevelOperation(string operationName)
    {
        switch (operationName)
        {
            case FhirValidateOperationService.OperationName:
                return serviceProvider.GetService<IFhirValidateOperationService>();
            default:
                return null;
        }
    }
    
    private IFhirTypeOperationService? GetTypeLevelOperation(string operationName)
    {
        switch (operationName)
        {
            case FhirValidateOperationService.OperationName:
                return serviceProvider.GetService<IFhirValidateOperationService>();
            default:
                return null;
        }
    }
    
    private IFhirInstanceOperationService? GetInstanceLevelOperation(string operationName)
    {
        switch (operationName)
        {
            case FhirValidateOperationService.OperationName:
                return serviceProvider.GetService<IFhirValidateOperationService>();
            default:
                return null;
        }
    }
}
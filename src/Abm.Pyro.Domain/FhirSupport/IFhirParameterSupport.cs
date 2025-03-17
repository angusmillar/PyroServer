using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.FhirSupport;

public interface IFhirParameterSupport
{
    public bool? GetParameterFhirBoolValue(
        string parameterName,
        List<Parameters.ParameterComponent> parameterComponentList);
    
    public Uri? GetParameterFhirUrlValue(
        string parameterName,
        List<Parameters.ParameterComponent> parameterComponentList);
    
}
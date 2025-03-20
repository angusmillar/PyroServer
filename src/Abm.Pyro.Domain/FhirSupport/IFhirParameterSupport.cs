using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.FhirSupport;

public interface IFhirParameterSupport
{
    public bool? GetParameterFhirBoolValue(Parameters.ParameterComponent parameterComponent);
    
    public Uri? GetParameterFhirUrlValue(Parameters.ParameterComponent parameterComponent);
    
    public Parameters.ParameterComponent? GetFirstParameterComponentByName(
        string parameterName,
        List<Parameters.ParameterComponent> parameterComponent);
    
}
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.FhirSupport;

public class FhirParameterSupport : IFhirParameterSupport
{
    public bool? GetParameterFhirBoolValue(Parameters.ParameterComponent parameterComponent)
    {
        if (parameterComponent?.Value is FhirBoolean fhirBoolean)
        {
            return fhirBoolean.Value;
        }

        return null;
    }
    public Uri? GetParameterFhirUrlValue(Parameters.ParameterComponent parameterComponent)
    {
        if (parameterComponent?.Value is FhirUrl fhirUrl)
        {
            if (Uri.TryCreate(fhirUrl.Value, UriKind.Absolute, out Uri? uri))
            {
                return uri;
            }
        }

        return null;
    }

    public Parameters.ParameterComponent? GetFirstParameterComponentByName(
        string parameterName,
        List<Parameters.ParameterComponent> parameterComponent)
    {
        return parameterComponent.FirstOrDefault(x => x.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));
    }
}
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.FhirSupport;

public class FhirParameterSupport : IFhirParameterSupport
{
    public bool? GetParameterFhirBoolValue(
        string parameterName,
        List<Parameters.ParameterComponent> parameterComponentList)
    {
        Parameters.ParameterComponent? parameterComponent = GetFirstParameterComponentByName(parameterName, parameterComponentList);

        if (parameterComponent?.Value is FhirBoolean fhirBoolean)
        {
            return fhirBoolean.Value;
        }

        return null;
    }

    public Uri? GetParameterFhirUrlValue(
        string parameterName,
        List<Parameters.ParameterComponent> parameterComponentList)
    {
        Parameters.ParameterComponent? parameterComponent = GetFirstParameterComponentByName(parameterName, parameterComponentList);

        if (parameterComponent?.Value is FhirUrl fhirUrl)
        {
            if (Uri.TryCreate(fhirUrl.Value, UriKind.Absolute, out Uri? uri))
            {
                return uri;
            }
        }

        return null;
    }

    private static Parameters.ParameterComponent? GetFirstParameterComponentByName(
        string parameterName,
        List<Parameters.ParameterComponent> parameterComponentList)
    {
        return parameterComponentList.FirstOrDefault(x => x.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));
    }
}
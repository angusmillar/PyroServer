namespace Abm.Pyro.Application.FhirClient;

public interface IFhirHttpClientFactory
{
    Hl7.Fhir.Rest.FhirClient CreateClient(string baseUrl);
}
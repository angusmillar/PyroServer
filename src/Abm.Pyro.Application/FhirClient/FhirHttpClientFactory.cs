using Hl7.Fhir.Rest;

namespace Abm.Pyro.Application.FhirClient;

public class FhirHttpClientFactory(HttpClient httpClient) : IFhirHttpClientFactory
{
    public Hl7.Fhir.Rest.FhirClient CreateClient(string baseUrl)
    {
        var fhirClientSettings = new FhirClientSettings()
        {
            PreferredFormat = ResourceFormat.Json,
        };

        return new Hl7.Fhir.Rest.FhirClient(baseUrl, httpClient, fhirClientSettings);
    }
}
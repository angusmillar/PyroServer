using Hl7.Fhir.Rest;

namespace Abm.Pyro.Application.FhirClient;

public class FhirHttpClientFactory(IHttpClientFactory httpClientFactory) : IFhirHttpClientFactory
{
    
    public const string HttpClientName = "FhirClient";
    
    public Hl7.Fhir.Rest.FhirClient CreateFhirClient(string baseUrl)
    {
        HttpClient httpClient = httpClientFactory.CreateClient(HttpClientName);
        httpClient.Timeout = TimeSpan.FromMinutes(20);
        var fhirClientSettings = new FhirClientSettings()
        {
            PreferredFormat = ResourceFormat.Json,
        };

        return new Hl7.Fhir.Rest.FhirClient(baseUrl, httpClient, fhirClientSettings);
    }
    
    public HttpClient CreateBasicClient(string baseUrl)
    {
        HttpClient httpClient = httpClientFactory.CreateClient(HttpClientName);
        httpClient.BaseAddress = new Uri(baseUrl);
        return httpClient;
    }
}
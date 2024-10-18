using Hl7.Fhir.Rest;

namespace Abm.Pyro.Application.FhirClient;

public class FhirHttpClientFactory(IHttpClientFactory httpClientFactory) : IFhirHttpClientFactory
{
    
    public const string HttpClientName = "FhirClient";
    private const int HttpClientTimeoutMinutes = 20;
    
    public Hl7.Fhir.Rest.FhirClient CreateFhirClient(string baseUrl)
    {
        var fhirClientSettings = new FhirClientSettings()
        {
            PreferredFormat = ResourceFormat.Json,
        };

        return new Hl7.Fhir.Rest.FhirClient(baseUrl, GetHttpClient(), fhirClientSettings);
    }

    public HttpClient CreateBasicClient(string baseUrl)
    {
        HttpClient httpClient = GetHttpClient();
        httpClient.BaseAddress = new Uri(baseUrl);
        return httpClient;
    }

    private HttpClient GetHttpClient()
    {
        HttpClient httpClient = httpClientFactory.CreateClient(HttpClientName);
        httpClient.Timeout = TimeSpan.FromMinutes(HttpClientTimeoutMinutes);
        return httpClient;
    }
}
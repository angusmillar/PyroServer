namespace Abm.Pyro.Application.FhirClient;

public interface IFhirHttpClientFactory
{
    Hl7.Fhir.Rest.FhirClient CreateFhirClient(string baseUrl);
    HttpClient CreateBasicClient(string baseUrl);

}
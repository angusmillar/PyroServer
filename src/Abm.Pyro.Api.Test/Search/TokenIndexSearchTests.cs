using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Search;

public class TokenIndexSearchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BodyWeightCode = "29463-7";
    private const string HeartRateCode = "8867-4";

    [Fact]
    public async Task Search_ByCode_ReturnsMatchingObservation()
    {
        await CreateObservationAsync(loincCode: BodyWeightCode);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code={CodeSystemUriSupport.Loinc}|{BodyWeightCode}" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByCode_NoMatch_ReturnsEmptyBundle()
    {
        await CreateObservationAsync(loincCode: BodyWeightCode);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code={CodeSystemUriSupport.Loinc}|{HeartRateCode}" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByCodeWithoutSystem_ReturnsMatchingObservation()
    {
        // FHIR token search with no system prefix matches on code alone.
        await CreateObservationAsync(loincCode: BodyWeightCode);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code={BodyWeightCode}" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }
    
    [Fact]
    public async Task Search_BySystemWithoutCode_ReturnsMatchingObservation()
    {
        // FHIR token search with no code matches on system alone.
        await CreateObservationAsync(loincCode: BodyWeightCode);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code={CodeSystemUriSupport.Loinc}|" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }
    
    [Fact]
    public async Task Search_BySystemOnly_ReturnsMatchingObservations()
    {
        // FHIR token search with no system prefix matches on code alone.
        await CreateObservationAsync(loincCode: BodyWeightCode);
        await CreateObservationAsync(loincCode: HeartRateCode);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code={CodeSystemUriSupport.Loinc}|" });

        Assert.NotNull(bundle);
        Assert.Equal(2, bundle.Entry.Count);
    }

    private async Task CreateObservationAsync(string? loincCode = null)
    {
        Observation? observation = await FhirClient.CreateAsync(
            ObservationBuilder.Build(loincCode: loincCode));
        Assert.NotNull(observation);
    }
}

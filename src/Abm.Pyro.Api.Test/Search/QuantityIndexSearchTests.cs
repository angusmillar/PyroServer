using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Search;

public class QuantityIndexSearchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Search_ByValueQuantity_ReturnsMatchingObservation()
    {
        await CreateObservationAsync(valueQuantityAmount: 80, valueQuantityUnit: "kg");

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"value-quantity=80|{CodeSystemUriSupport.Ucum}|kg" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByValueQuantity_NoMatch_ReturnsEmptyBundle()
    {
        await CreateObservationAsync(valueQuantityAmount: 80, valueQuantityUnit: "kg");

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"value-quantity=70|{CodeSystemUriSupport.Ucum}|kg" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByValueQuantity_OnlyReturnsObservationsWithMatchingValue()
    {
        await CreateObservationAsync(valueQuantityAmount: 80, valueQuantityUnit: "kg");
        await CreateObservationAsync(valueQuantityAmount: 70, valueQuantityUnit: "kg");

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"value-quantity=80|{CodeSystemUriSupport.Ucum}|kg" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_ValueQuantityMissingTrue_ReturnsOnlyObservationWithoutValue()
    {
        Observation? withValue = await FhirClient.CreateAsync(
            ObservationBuilder.Build(valueQuantityAmount: 72.5m));
        Observation? withoutValue = await FhirClient.CreateAsync(ObservationBuilder.Build());
        Assert.NotNull(withValue);
        Assert.NotNull(withoutValue);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { "value-quantity:missing=true" });

        Assert.NotNull(bundle);
        Assert.Equal([withoutValue.Id], bundle.Entry.Select(e => e.Resource!.Id).Order());
    }

    [Fact]
    public async Task Search_ValueQuantityMissingFalse_ReturnsOnlyObservationWithValue()
    {
        Observation? withValue = await FhirClient.CreateAsync(
            ObservationBuilder.Build(valueQuantityAmount: 72.5m));
        Observation? withoutValue = await FhirClient.CreateAsync(ObservationBuilder.Build());
        Assert.NotNull(withValue);
        Assert.NotNull(withoutValue);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { "value-quantity:missing=false" });

        Assert.NotNull(bundle);
        Assert.Equal([withValue.Id], bundle.Entry.Select(e => e.Resource!.Id).Order());
    }

    private async Task CreateObservationAsync(decimal? valueQuantityAmount = null, string? valueQuantityUnit = null)
    {
        Observation? observation = await FhirClient.CreateAsync(
            ObservationBuilder.Build(
                valueQuantityAmount: valueQuantityAmount,
                valueQuantityUnit: valueQuantityUnit));
        Assert.NotNull(observation);
    }
}

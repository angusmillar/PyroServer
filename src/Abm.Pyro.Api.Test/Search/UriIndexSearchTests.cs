using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;

namespace Abm.Pyro.Api.Test.Search;

public class UriIndexSearchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string TestUrl = "http://example.org/fhir/ValueSet/test-colours";
    private const string OtherUrl = "http://example.org/fhir/ValueSet/test-animals";

    [Fact]
    public async Task Search_ByUrl_ReturnsMatchingValueSet()
    {
        await CreateValueSetAsync(url: TestUrl);

        Hl7.Fhir.Model.Bundle? bundle = await FhirClient.SearchAsync<Hl7.Fhir.Model.ValueSet>(
            new[] { $"url={TestUrl}" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByUrl_NoMatch_ReturnsEmptyBundle()
    {
        await CreateValueSetAsync(url: TestUrl);

        Hl7.Fhir.Model.Bundle? bundle = await FhirClient.SearchAsync<Hl7.Fhir.Model.ValueSet>(
            new[] { "url=http://example.org/fhir/ValueSet/does-not-exist" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByUrl_OnlyReturnsValueSetWithMatchingUrl()
    {
        await CreateValueSetAsync(url: TestUrl, name: "ColourValueSet");
        await CreateValueSetAsync(url: OtherUrl, name: "AnimalValueSet");

        Hl7.Fhir.Model.Bundle? bundle = await FhirClient.SearchAsync<Hl7.Fhir.Model.ValueSet>(
            new[] { $"url={TestUrl}" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
        var returned = Assert.IsType<Hl7.Fhir.Model.ValueSet>(bundle.Entry[0].Resource);
        Assert.Equal("ColourValueSet", returned.Name);
    }

    private async Task CreateValueSetAsync(string? url = null, string? name = null)
    {
        Hl7.Fhir.Model.ValueSet? valueSet = await FhirClient.CreateAsync(
            ValueSetBuilder.Build(url: url, name: name));
        Assert.NotNull(valueSet);
    }
}

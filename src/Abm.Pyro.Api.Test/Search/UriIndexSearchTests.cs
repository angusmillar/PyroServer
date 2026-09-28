using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Search;

public class UriIndexSearchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string TestUrl = "http://example.org/fhir/ValueSet/test-colours";
    private const string OtherUrl = "http://example.org/fhir/ValueSet/test-animals";

    [Fact]
    public async Task Search_ByUrl_ReturnsMatchingValueSet()
    {
        await CreateValueSetAsync(url: TestUrl);

        Bundle? bundle = await FhirClient.SearchAsync<ValueSet>(
            new[] { $"url={TestUrl}" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByUrl_NoMatch_ReturnsEmptyBundle()
    {
        await CreateValueSetAsync(url: TestUrl);

        Bundle? bundle = await FhirClient.SearchAsync<ValueSet>(
            new[] { "url=http://example.org/fhir/ValueSet/does-not-exist" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByUrl_OnlyReturnsValueSetWithMatchingUrl()
    {
        await CreateValueSetAsync(url: TestUrl, name: "ColourValueSet");
        await CreateValueSetAsync(url: OtherUrl, name: "AnimalValueSet");

        Bundle? bundle = await FhirClient.SearchAsync<ValueSet>(
            new[] { $"url={TestUrl}" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
        var returned = Assert.IsType<ValueSet>(bundle.Entry[0].Resource);
        Assert.Equal("ColourValueSet", returned.Name);
    }

    [Fact]
    public async Task Search_UrlMissingTrue_ReturnsOnlyValueSetWithoutUrl()
    {
        ValueSet? withUrl = await FhirClient.CreateAsync(ValueSetBuilder.Build(url: TestUrl));
        ValueSet? withoutUrl = await FhirClient.CreateAsync(ValueSetBuilder.Build(includeUrl: false));
        Assert.NotNull(withUrl);
        Assert.NotNull(withoutUrl);

        Bundle? bundle = await FhirClient.SearchAsync<ValueSet>(new[] { "url:missing=true" });

        Assert.NotNull(bundle);
        Assert.Equal([withoutUrl.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    [Fact]
    public async Task Search_UrlMissingFalse_ReturnsOnlyValueSetWithUrl()
    {
        ValueSet? withUrl = await FhirClient.CreateAsync(ValueSetBuilder.Build(url: TestUrl));
        ValueSet? withoutUrl = await FhirClient.CreateAsync(ValueSetBuilder.Build(includeUrl: false));
        Assert.NotNull(withUrl);
        Assert.NotNull(withoutUrl);

        Bundle? bundle = await FhirClient.SearchAsync<ValueSet>(new[] { "url:missing=false" });

        Assert.NotNull(bundle);
        Assert.Equal([withUrl.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    private async Task CreateValueSetAsync(string? url = null, string? name = null)
    {
        ValueSet? valueSet = await FhirClient.CreateAsync(
            ValueSetBuilder.Build(url: url, name: name));
        Assert.NotNull(valueSet);
    }
}

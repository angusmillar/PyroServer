using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Search;

public class StringIndexSearchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Search_ByFamilyName_ReturnsMatchingPatient()
    {
        await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Smith"));

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "family=Smith" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
        Assert.IsType<Patient>(bundle.Entry.Single().Resource);
    }

    [Fact]
    public async Task Search_ByFamilyName_NoMatch_ReturnsEmptyBundle()
    {
        await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Smith"));

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "family=Jones" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByFamilyName_OnlyReturnsMatchingPatients()
    {
        await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Smith"));
        await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Jones"));

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "family=Smith" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
        var returned = Assert.IsType<Patient>(bundle.Entry.Single().Resource);
        Assert.Equal("Smith", returned.Name.First().Family);
    }

    [Fact]
    public async Task Search_ByFamilyNameExact_ReturnsMatchingPatient()
    {
        await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Smiths"));
        await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Smith"));

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "family:exact=Smith" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
        var returned = Assert.IsType<Patient>(bundle.Entry.Single().Resource);
        Assert.Equal("Smith", returned.Name.First().Family);  
    }

    [Fact]
    public async Task Search_ByFamilyNameExact_NoMatch_ReturnsEmptyBundle()
    {
        await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Smith"));

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "family:exact=Jones" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByFamilyNameExact_PartialValue_ReturnsEmptyBundle()
    {
        // Unlike the default modifier (which matches on StartsWith/EndsWith), :exact requires
        // a full-value match, so a prefix of the stored value must not match.
        await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Smith"));

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "family:exact=Smit" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByFamilyNameContains_ReturnsMatchingPatient()
    {
        // "mit" is a substring in the middle of "Smith" - neither a prefix nor a suffix -
        // so only the :contains modifier (not the default StartsWith/EndsWith behavior) matches it.
        await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Smith"));

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "family:contains=mit" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByFamilyNameContains_NoMatch_ReturnsEmptyBundle()
    {
        await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Smith"));

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "family:contains=xyz" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByFamilyNameContains_OnlyReturnsMatchingPatients()
    {
        await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Smith"));
        await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Jones"));

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "family:contains=mit" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
        var returned = Assert.IsType<Patient>(bundle.Entry.Single().Resource);
        Assert.Equal("Smith", returned.Name.First().Family);
    }
}

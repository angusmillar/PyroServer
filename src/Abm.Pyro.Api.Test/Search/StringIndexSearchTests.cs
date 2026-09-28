using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
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

    [Fact]
    public async Task Search_NameMissingTrue_ReturnsOnlyPatientWithoutName()
    {
        Patient? withName = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Smith"));
        Patient? withoutName = await FhirClient.CreateAsync(PatientBuilder.Build(includeName: false));
        Assert.NotNull(withName);
        Assert.NotNull(withoutName);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(new[] { "name:missing=true" });

        Assert.NotNull(bundle);
        Assert.Equal([withoutName.Id], bundle.Entry.Select(e => e.Resource!.Id).Order());
    }

    [Fact]
    public async Task Search_NameMissingFalse_ReturnsOnlyPatientWithName()
    {
        Patient? withName = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Smith"));
        Patient? withoutName = await FhirClient.CreateAsync(PatientBuilder.Build(includeName: false));
        Assert.NotNull(withName);
        Assert.NotNull(withoutName);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(new[] { "name:missing=false" });

        Assert.NotNull(bundle);
        Assert.Equal([withName.Id], bundle.Entry.Select(e => e.Resource!.Id).Order());
    }

    [Fact]
    public async Task Search_NameMissingTrue_ExcludesDeletedResource()
    {
        // Review Focus 1: NOT EXISTS must compose with the IsCurrent/IsDeleted filter.
        Patient? deleted = await FhirClient.CreateAsync(PatientBuilder.Build(includeName: false));
        Assert.NotNull(deleted);
        await FhirClient.DeleteAsync($"Patient/{deleted.Id}");

        Patient? live = await FhirClient.CreateAsync(PatientBuilder.Build(includeName: false));
        Assert.NotNull(live);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(new[] { "name:missing=true" });

        Assert.NotNull(bundle);
        Assert.Equal([live.Id], bundle.Entry.Select(e => e.Resource!.Id).Order());
    }

    [Fact]
    public async Task Search_NameMissingWithNonBooleanValue_ReturnsBadRequest()
    {
        // Review Focus 2: a bad :missing value must stay a 400, not degrade to false.
        FhirOperationException exception = await Assert.ThrowsAsync<FhirOperationException>(
            () => FhirClient.SearchAsync<Patient>(new[] { "name:missing=maybe" }));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, exception.Status);
    }
}

using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;

namespace Abm.Pyro.Api.Test.Search;

public class StringIndexSearchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Search_ByFamilyName_ReturnsMatchingPatient()
    {
        await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Smith"));

        Hl7.Fhir.Model.Bundle? bundle = await FhirClient.SearchAsync<Hl7.Fhir.Model.Patient>(
            new[] { "family=Smith" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
        Assert.IsType<Hl7.Fhir.Model.Patient>(bundle.Entry.Single().Resource);
    }

    [Fact]
    public async Task Search_ByFamilyName_NoMatch_ReturnsEmptyBundle()
    {
        await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Smith"));

        Hl7.Fhir.Model.Bundle? bundle = await FhirClient.SearchAsync<Hl7.Fhir.Model.Patient>(
            new[] { "family=Jones" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByFamilyName_OnlyReturnsMatchingPatients()
    {
        await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Smith"));
        await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Jones"));

        Hl7.Fhir.Model.Bundle? bundle = await FhirClient.SearchAsync<Hl7.Fhir.Model.Patient>(
            new[] { "family=Smith" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
        var returned = Assert.IsType<Hl7.Fhir.Model.Patient>(bundle.Entry.Single().Resource);
        Assert.Equal("Smith", returned.Name.First().Family);
    }
}

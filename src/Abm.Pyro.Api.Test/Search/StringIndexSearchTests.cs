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
}

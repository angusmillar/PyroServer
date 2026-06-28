using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;

namespace Abm.Pyro.Api.Test.Search;

public class DateTimeIndexSearchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string DeceasedDate = "2023-01-15";

    [Fact]
    public async Task Search_ByDeathDate_ReturnsDeceasedPatient()
    {
        await FhirClient.CreateAsync(PatientBuilder.Build(deceasedDateTime: DeceasedDate));

        Hl7.Fhir.Model.Bundle? bundle = await FhirClient.SearchAsync<Hl7.Fhir.Model.Patient>(
            new[] { $"death-date={DeceasedDate}" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByDeathDate_NoMatch_ReturnsEmptyBundle()
    {
        await FhirClient.CreateAsync(PatientBuilder.Build(deceasedDateTime: DeceasedDate));

        Hl7.Fhir.Model.Bundle? bundle = await FhirClient.SearchAsync<Hl7.Fhir.Model.Patient>(
            new[] { "death-date=2023-06-01" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByDeathDate_AlivePatient_ReturnsEmptyBundle()
    {
        // A patient with no deceasedDateTime should not appear in death-date searches.
        await FhirClient.CreateAsync(PatientBuilder.Build());

        Hl7.Fhir.Model.Bundle? bundle = await FhirClient.SearchAsync<Hl7.Fhir.Model.Patient>(
            new[] { $"death-date={DeceasedDate}" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }
}

using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Search;

public class DateTimeIndexSearchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string DeceasedDate = "2023-01-15";

    [Fact]
    public async Task Search_ByDeathDate_ReturnsDeceasedPatient()
    {
        await FhirClient.CreateAsync(PatientBuilder.Build(deceasedDateTime: DeceasedDate));

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { $"death-date={DeceasedDate}" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByDeathDate_NoMatch_ReturnsEmptyBundle()
    {
        await FhirClient.CreateAsync(PatientBuilder.Build(deceasedDateTime: DeceasedDate));

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "death-date=2023-06-01" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByDeathDate_AlivePatient_ReturnsEmptyBundle()
    {
        // A patient with no deceasedDateTime should not appear in death-date searches.
        await FhirClient.CreateAsync(PatientBuilder.Build());

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { $"death-date={DeceasedDate}" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_BirthDateMissingTrue_ReturnsOnlyPatientWithoutBirthDate()
    {
        Patient? withDate = await FhirClient.CreateAsync(PatientBuilder.Build());
        Patient? withoutDate = await FhirClient.CreateAsync(PatientBuilder.Build(includeBirthDate: false));
        Assert.NotNull(withDate);
        Assert.NotNull(withoutDate);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(new[] { "birthdate:missing=true" });

        Assert.NotNull(bundle);
        Assert.Equal([withoutDate.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    [Fact]
    public async Task Search_BirthDateNotEqual_ExcludesPatientWithoutBirthDate()
    {
        // 'ne' is a comparison: a resource with no birthdate cannot satisfy it. Absence is
        // what ':missing' is for. The bug being fixed here OR'd in 'spid <> @x', which matched
        // any resource carrying an index row for some other parameter.
        Patient? matching = await FhirClient.CreateAsync(PatientBuilder.Build());          // 1990-01-15
        Patient? withoutDate = await FhirClient.CreateAsync(PatientBuilder.Build(includeBirthDate: false));
        Assert.NotNull(matching);
        Assert.NotNull(withoutDate);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(new[] { "birthdate=ne2001-02-03" });

        Assert.NotNull(bundle);
        Assert.Equal([matching.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }
}

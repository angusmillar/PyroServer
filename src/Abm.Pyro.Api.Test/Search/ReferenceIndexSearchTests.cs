using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Search;

public class ReferenceIndexSearchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Search_BySubjectReference_ReturnsMatchingObservation()
    {
        Patient patient = await CreatePatientAsync();
        await CreateObservationAsync(subjectPatientId: patient.Id);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"subject=Patient/{patient.Id}" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_BySubjectReference_NoMatch_ReturnsEmptyBundle()
    {
        Patient patient = await CreatePatientAsync();
        await CreateObservationAsync(subjectPatientId: patient.Id);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { "subject=Patient/does-not-exist-99999" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_BySubjectReference_OnlyReturnsObservationsForSpecificPatient()
    {
        Patient patientA = await CreatePatientAsync();
        Patient patientB = await CreatePatientAsync();
        await CreateObservationAsync(subjectPatientId: patientA.Id);
        await CreateObservationAsync(subjectPatientId: patientB.Id);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"subject=Patient/{patientA.Id}" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
        Assert.IsType<Observation>(bundle.Entry.Single().Resource);
        if (bundle.Entry.First().Resource is Observation observation)
        {
            Assert.Equal($"Patient/{patientA.Id}", observation.Subject.Reference);
        }
    }

    private async Task<Patient> CreatePatientAsync()
    {
        Patient? patient = await FhirClient.CreateAsync(PatientBuilder.Build());
        Assert.NotNull(patient);
        return patient;
    }

    private async Task CreateObservationAsync(string? subjectPatientId = null)
    {
        Observation? observation = await FhirClient.CreateAsync(
            ObservationBuilder.Build(subjectPatientId: subjectPatientId));
        Assert.NotNull(observation);
    }
}

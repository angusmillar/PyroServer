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
        Hl7.Fhir.Model.Patient patient = await CreatePatientAsync();
        await CreateObservationAsync(subjectPatientId: patient.Id);

        Bundle? bundle = await FhirClient.SearchAsync<Hl7.Fhir.Model.Observation>(
            new[] { $"subject=Patient/{patient.Id}" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_BySubjectReference_NoMatch_ReturnsEmptyBundle()
    {
        Hl7.Fhir.Model.Patient patient = await CreatePatientAsync();
        await CreateObservationAsync(subjectPatientId: patient.Id);

        Hl7.Fhir.Model.Bundle? bundle = await FhirClient.SearchAsync<Hl7.Fhir.Model.Observation>(
            new[] { "subject=Patient/does-not-exist-99999" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_BySubjectReference_OnlyReturnsObservationsForSpecificPatient()
    {
        Hl7.Fhir.Model.Patient patientA = await CreatePatientAsync();
        Hl7.Fhir.Model.Patient patientB = await CreatePatientAsync();
        await CreateObservationAsync(subjectPatientId: patientA.Id);
        await CreateObservationAsync(subjectPatientId: patientB.Id);

        Hl7.Fhir.Model.Bundle? bundle = await FhirClient.SearchAsync<Hl7.Fhir.Model.Observation>(
            new[] { $"subject=Patient/{patientA.Id}" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
        Assert.IsType<Observation>(bundle.Entry.Single().Resource);
        if (bundle.Entry.First().Resource is Hl7.Fhir.Model.Observation observation)
        {
            Assert.Equal($"Patient/{patientA.Id}", observation.Subject.Reference);
        }
    }

    private async Task<Hl7.Fhir.Model.Patient> CreatePatientAsync()
    {
        Hl7.Fhir.Model.Patient? patient = await FhirClient.CreateAsync(PatientBuilder.Build());
        Assert.NotNull(patient);
        return patient;
    }

    private async Task CreateObservationAsync(string? subjectPatientId = null)
    {
        Hl7.Fhir.Model.Observation? observation = await FhirClient.CreateAsync(
            ObservationBuilder.Build(subjectPatientId: subjectPatientId));
        Assert.NotNull(observation);
    }
}

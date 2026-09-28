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
            Assert.Equal($"Patient/{patientA.Id}", observation.Subject?.Reference);
        }
    }

    [Fact]
    public async Task Search_OrganizationMissingTrue_ReturnsOnlyPatientWithoutOrganization()
    {
        Organization? organization = await FhirClient.CreateAsync(OrganizationBuilder.Build());
        Assert.NotNull(organization);

        Patient? withOrganization = await FhirClient.CreateAsync(
            PatientBuilder.Build(managingOrganizationId: organization.Id));
        Patient? withoutOrganization = await FhirClient.CreateAsync(PatientBuilder.Build());
        Assert.NotNull(withOrganization);
        Assert.NotNull(withoutOrganization);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "organization:missing=true" });

        Assert.NotNull(bundle);
        Assert.Equal([withoutOrganization.Id], bundle.Entry.Select(e => e.Resource!.Id).Order());
    }

    [Fact]
    public async Task Search_OrganizationMissingFalse_ReturnsOnlyPatientWithOrganization()
    {
        Organization? organization = await FhirClient.CreateAsync(OrganizationBuilder.Build());
        Assert.NotNull(organization);

        Patient? withOrganization = await FhirClient.CreateAsync(
            PatientBuilder.Build(managingOrganizationId: organization.Id));
        Patient? withoutOrganization = await FhirClient.CreateAsync(PatientBuilder.Build());
        Assert.NotNull(withOrganization);
        Assert.NotNull(withoutOrganization);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "organization:missing=false" });

        Assert.NotNull(bundle);
        Assert.Equal([withOrganization.Id], bundle.Entry.Select(e => e.Resource!.Id).Order());
    }

    [Fact]
    public async Task Search_OrganizationMissingFalseThenTrue_ReturnsEveryPatient()
    {
        // 'missing=false,true' is "present OR absent" -- a tautology, like near:missing=true,false.
        // The value ORDER matters: the multi-id fast-path guard tests !IsMissing first, so
        // 'true,false' short-circuits harmlessly while 'false,true' reaches a FhirUri that is null
        // for a ':missing' value. That dereference was a 500.
        Organization? organization = await FhirClient.CreateAsync(OrganizationBuilder.Build());
        Assert.NotNull(organization);

        Patient? withOrganization = await FhirClient.CreateAsync(
            PatientBuilder.Build(managingOrganizationId: organization.Id));
        Patient? withoutOrganization = await FhirClient.CreateAsync(PatientBuilder.Build());
        Assert.NotNull(withOrganization);
        Assert.NotNull(withoutOrganization);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "organization:missing=false,true" });

        Assert.NotNull(bundle);
        Assert.Equal(
            new[] { withOrganization.Id, withoutOrganization.Id }.Order(),
            bundle.Entry.Select(e => e.Resource!.Id).Order());
    }

    [Fact]
    public async Task Search_OrganizationMissingWithNonBooleanValue_ReturnsBadRequest()
    {
        // Review Focus 2, for the Reference type. SearchQueryReference was the only one of the
        // seven that set IsValid = false and then fell through to dereference the bool? it had
        // just proved null, turning a malformed value into a 500.
        Hl7.Fhir.Rest.FhirOperationException exception =
            await Assert.ThrowsAsync<Hl7.Fhir.Rest.FhirOperationException>(
                () => FhirClient.SearchAsync<Patient>(new[] { "organization:missing=maybe" }));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, exception.Status);
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

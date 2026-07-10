using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Chaining;

/// <summary>
/// Forward chained search parameters: https://hl7.org/fhir/R4/search.html#chaining
/// </summary>
public class ChainedSearchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Search_ByChainedSubjectFamilyName_ReturnsMatchingObservation()
    {
        Patient patient = await CreatePatientAsync(familyName: "Smith");
        await CreateObservationAsync(subjectPatientId: patient.Id);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { "subject.family=Smith" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByChainedSubjectFamilyName_NoMatch_ReturnsEmptyBundle()
    {
        Patient patient = await CreatePatientAsync(familyName: "Smith");
        await CreateObservationAsync(subjectPatientId: patient.Id);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { "subject.family=Jones" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByChainedSubjectFamilyNameWithTypeModifier_ReturnsMatchingObservation()
    {
        Patient patient = await CreatePatientAsync(familyName: "Smith");
        await CreateObservationAsync(subjectPatientId: patient.Id);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { "subject:Patient.family=Smith" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByChainedPatientFamilyName_ReturnsMatchingObservation()
    {
        Patient patient = await CreatePatientAsync(familyName: "Smith");
        await CreateObservationAsync(subjectPatientId: patient.Id);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { "patient.family=Smith" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByChainedSubjectGivenName_ReturnsMatchingObservation()
    {
        Patient patient = await CreatePatientAsync(givenName: "Alice");
        await CreateObservationAsync(subjectPatientId: patient.Id);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { "subject.given=Alice" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByChainedSubjectFamilyName_OnlyReturnsObservationsForMatchingPatient()
    {
        Patient patientSmith = await CreatePatientAsync(familyName: "Smith");
        Patient patientJones = await CreatePatientAsync(familyName: "Jones");
        await CreateObservationAsync(subjectPatientId: patientSmith.Id);
        await CreateObservationAsync(subjectPatientId: patientJones.Id);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { "subject.family=Smith" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
        Assert.IsType<Observation>(bundle.Entry.Single().Resource);
        if (bundle.Entry.Single().Resource is Observation observation)
        {
            Assert.Equal($"Patient/{patientSmith.Id}", observation.Subject.Reference);
        }
    }

    [Fact]
    public async Task Search_ByChainedSubjectOrganizationName_ReturnsMatchingObservation()
    {
        // Two-hop chain: Observation.subject -> Patient.organization -> Organization.name
        // Observation.subject targets Group|Device|Location|Patient, three of which (Device, Location,
        // Patient) define an "organization" search parameter, so a type modifier is required to
        // disambiguate which target resource type the next chain segment applies to.
        Organization organization = await CreateOrganizationAsync(name: "AcmeHealth");
        Patient patient = await CreatePatientAsync(managingOrganizationId: organization.Id);
        await CreateObservationAsync(subjectPatientId: patient.Id);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { "subject:Patient.organization.name=AcmeHealth" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByChainedSubjectOrganizationName_NoMatch_ReturnsEmptyBundle()
    {
        Organization organization = await CreateOrganizationAsync(name: "AcmeHealth");
        Patient patient = await CreatePatientAsync(managingOrganizationId: organization.Id);
        await CreateObservationAsync(subjectPatientId: patient.Id);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { "subject:Patient.organization.name=OtherHealth" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByChainedSubjectOrganizationName_OnlyReturnsObservationsForMatchingOrganization()
    {
        Organization organizationAcme = await CreateOrganizationAsync(name: "AcmeHealth");
        Organization organizationOther = await CreateOrganizationAsync(name: "OtherHealth");
        Patient patientAcme = await CreatePatientAsync(managingOrganizationId: organizationAcme.Id);
        Patient patientOther = await CreatePatientAsync(managingOrganizationId: organizationOther.Id);
        await CreateObservationAsync(subjectPatientId: patientAcme.Id);
        await CreateObservationAsync(subjectPatientId: patientOther.Id);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { "subject:Patient.organization.name=AcmeHealth" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
        Assert.IsType<Observation>(bundle.Entry.Single().Resource);
        if (bundle.Entry.Single().Resource is Observation observation)
        {
            Assert.Equal($"Patient/{patientAcme.Id}", observation.Subject.Reference);
        }
    }

    private async Task<Patient> CreatePatientAsync(
        string? familyName = null,
        string? givenName = null,
        string? managingOrganizationId = null)
    {
        Patient? patient = await FhirClient.CreateAsync(
            PatientBuilder.Build(familyName: familyName, givenName: givenName, managingOrganizationId: managingOrganizationId));
        Assert.NotNull(patient);
        return patient;
    }

    private async Task<Organization> CreateOrganizationAsync(string? name = null)
    {
        Organization? organization = await FhirClient.CreateAsync(OrganizationBuilder.Build(name: name));
        Assert.NotNull(organization);
        return organization;
    }

    private async Task CreateObservationAsync(string? subjectPatientId = null)
    {
        Observation? observation = await FhirClient.CreateAsync(
            ObservationBuilder.Build(subjectPatientId: subjectPatientId));
        Assert.NotNull(observation);
    }
}

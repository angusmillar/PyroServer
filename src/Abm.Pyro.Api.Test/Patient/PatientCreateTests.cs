using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;

namespace Abm.Pyro.Api.Test.Patient;

public class PatientCreateTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Create_ValidPatient_Returns201WithLocationHeader()
    {
        Hl7.Fhir.Model.Patient patient = PatientBuilder.Build();

        Hl7.Fhir.Model.Patient? patientResponse = await FhirClient.CreateAsync(patient);

        Assert.NotNull(patientResponse);
    }

    [Fact]
    public async Task Create_ValidPatient_ReturnsPatientResourceInBody()
    {
        Hl7.Fhir.Model.Patient patient = PatientBuilder.Build(familyName: "Smith");

        Hl7.Fhir.Model.Patient? returned = await FhirClient.CreateAsync(patient);

        Assert.NotNull(returned);
        Assert.NotNull(returned.Id);
        Assert.Equal("Smith", returned.Name.First().Family);
    }

    [Fact]
    public async Task Create_ValidPatient_AssignsServerGeneratedId()
    {
        Hl7.Fhir.Model.Patient patient = PatientBuilder.Build();

        Hl7.Fhir.Model.Patient? returned = await FhirClient.CreateAsync(patient);

        Assert.NotNull(returned);
        Assert.NotNull(returned.Id);
        Assert.NotEmpty(returned.Id);
    }
}

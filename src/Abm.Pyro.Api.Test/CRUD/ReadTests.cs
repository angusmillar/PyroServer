using System.Net;
using Hl7.Fhir.Rest;
using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;

namespace Abm.Pyro.Api.Test.CRUD;

public class ReadTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Read_ExistingPatient_Returns200WithResource()
    {
        // Arrange — create first
        Hl7.Fhir.Model.Patient created = await CreatePatientAsync("Jones");

        // Act
        Hl7.Fhir.Model.Patient? returned = await FhirClient.ReadAsync<Hl7.Fhir.Model.Patient>($"Patient/{created.Id}");

        Assert.NotNull(returned);
        Assert.Equal(created.Id, returned.Id);
        Assert.Equal("Jones", returned.Name.First().Family);
    }

    [Fact]
    public async Task Read_NonExistentId_Returns404()
    {
        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => FhirClient.ReadAsync<Hl7.Fhir.Model.Patient>("Patient/does-not-exist-12345"));

        Assert.Equal(HttpStatusCode.NotFound, ex.Status);
    }

    [Fact]
    public async Task Read_DeletedPatient_Returns410Gone()
    {
        // Arrange — create then delete
        Hl7.Fhir.Model.Patient created = await CreatePatientAsync();
        await FhirClient.DeleteAsync(created);

        // Act
        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => FhirClient.ReadAsync<Hl7.Fhir.Model.Patient>($"Patient/{created.Id}"));

        Assert.Equal(HttpStatusCode.Gone, ex.Status);
    }

    private async Task<Hl7.Fhir.Model.Patient> CreatePatientAsync(string? familyName = null)
    {
        Hl7.Fhir.Model.Patient? created = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: familyName));
        Assert.NotNull(created);
        return created;
    }
}

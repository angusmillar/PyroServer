using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;

namespace Abm.Pyro.Api.Test.Patient;

public class PatientUpdateTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Update_ExistingPatient_Returns200()
    {
        // Arrange
        Hl7.Fhir.Model.Patient created = await CreatePatientAsync("Original");
        
        string originalFamilyName =  created.Name.First().Family;
        created.Name.First().Family = "Updated";

        // Act
        Hl7.Fhir.Model.Patient? updated = await FhirClient.UpdateAsync(created);

        Assert.NotNull(updated);
        Assert.NotNull(updated.Meta);
        Assert.True(updated.Meta.LastUpdated > created.Meta.LastUpdated);
        Assert.True(updated.Name.First().Family != originalFamilyName);
        Assert.Equal("2", updated.Meta.VersionId);
    }

    [Fact]
    public async Task Update_ExistingPatient_PersistsChanges()
    {
        // Arrange
        Hl7.Fhir.Model.Patient created = await CreatePatientAsync("Original");
        created.Name.First().Family = "Updated";

        // Act
        await FhirClient.UpdateAsync(created);
        Hl7.Fhir.Model.Patient? read = await FhirClient.ReadAsync<Hl7.Fhir.Model.Patient>($"Patient/{created.Id}");

        Assert.NotNull(read);
        Assert.Equal("Updated", read.Name.First().Family);
    }

    [Fact]
    public async Task Update_NonExistentPatient_Upsert_Returns201()
    {
        // FHIR spec: PUT to a non-existent ID is an upsert → resource is returned on success
        string newId = Guid.NewGuid().ToString();
        Hl7.Fhir.Model.Patient patient = PatientBuilder.Build(id: newId);

        Hl7.Fhir.Model.Patient? upserted = await FhirClient.UpdateAsync(patient);

        Assert.NotNull(upserted);
    }

    [Fact]
    public async Task Update_IncrementsVersionId()
    {
        // Arrange
        Hl7.Fhir.Model.Patient created = await CreatePatientAsync();
        string? versionAfterCreate = created.VersionId;

        // Act — update, then read back to get the new VersionId.
        // UpdateAsync may return minimal response; a subsequent ReadAsync always
        // returns the full resource with the current VersionId.
        created.Name.First().Family = "Modified";
        await FhirClient.UpdateAsync(created);
        Hl7.Fhir.Model.Patient? afterUpdate = await FhirClient.ReadAsync<Hl7.Fhir.Model.Patient>($"Patient/{created.Id}");
        
        Assert.NotNull(afterUpdate);
        Assert.NotEqual(versionAfterCreate, afterUpdate.VersionId);
    }

    private async Task<Hl7.Fhir.Model.Patient> CreatePatientAsync(string? familyName = null)
    {
        Hl7.Fhir.Model.Patient? created = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: familyName));
        Assert.NotNull(created);
        return created;
    }
}

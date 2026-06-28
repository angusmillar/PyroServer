using System.Net;
using Hl7.Fhir.Rest;
using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;

namespace Abm.Pyro.Api.Test.CRUD;

public class DeleteTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Delete_ExistingPatient_Returns204()
    {
        // Arrange
        Hl7.Fhir.Model.Patient created = await CreatePatientAsync();

        // FhirClient.DeleteAsync completes without exception on success (204 No Content)
        await FhirClient.DeleteAsync(created);
    }

    [Fact]
    public async Task Delete_ExistingPatient_SubsequentReadReturns410()
    {
        // Arrange
        Hl7.Fhir.Model.Patient created = await CreatePatientAsync();
        await FhirClient.DeleteAsync(created);

        // Act
        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => FhirClient.ReadAsync<Hl7.Fhir.Model.Patient>($"Patient/{created.Id}"));

        Assert.Equal(HttpStatusCode.Gone, ex.Status);
    }

    [Fact]
    public async Task Delete_NonExistentPatient_Returns204()
    {
        // Pyro treats DELETE as idempotent: deleting a resource that never existed
        // returns 204 No Content rather than 404, so no exception is thrown.
        var nonExistentPatient = new Hl7.Fhir.Model.Patient { Id = "non-existent-id-99999" };

        await FhirClient.DeleteAsync(nonExistentPatient);
    }

    private async Task<Hl7.Fhir.Model.Patient> CreatePatientAsync(string? familyName = null)
    {
        Hl7.Fhir.Model.Patient? created = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: familyName));
        Assert.NotNull(created);
        return created;
    }
}

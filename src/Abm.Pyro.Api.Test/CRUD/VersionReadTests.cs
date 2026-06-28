using System.Net;
using Hl7.Fhir.Rest;
using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;

namespace Abm.Pyro.Api.Test.CRUD;

public class VersionReadTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    // === vread: GET [base]/Patient/[id]/_history/[vid] ===

    [Fact]
    public async Task VRead_CurrentVersion_ReturnsResource()
    {
        Hl7.Fhir.Model.Patient created = await CreatePatientAsync("Jones");

        Hl7.Fhir.Model.Patient? vread = await FhirClient.ReadAsync<Hl7.Fhir.Model.Patient>(
            $"Patient/{created.Id}/_history/1");

        Assert.NotNull(vread);
        Assert.Equal("1", vread.VersionId);
        Assert.Equal("Jones", vread.Name.First().Family);
    }

    [Fact]
    public async Task VRead_AfterUpdate_HistoricalVersionPreservesOriginalContent()
    {
        // vread of version 1 after an update must return the original content, not the updated content.
        Hl7.Fhir.Model.Patient created = await CreatePatientAsync("Original");
        created.Name.First().Family = "Updated";
        await FhirClient.UpdateAsync(created);

        Hl7.Fhir.Model.Patient? historical = await FhirClient.ReadAsync<Hl7.Fhir.Model.Patient>(
            $"Patient/{created.Id}/_history/1");

        Assert.NotNull(historical);
        Assert.Equal("1", historical.VersionId);
        Assert.Equal("Original", historical.Name.First().Family);
    }

    [Fact]
    public async Task VRead_AfterUpdate_CurrentVersionReturnsUpdatedContent()
    {
        Hl7.Fhir.Model.Patient created = await CreatePatientAsync("Original");
        created.Name.First().Family = "Updated";
        await FhirClient.UpdateAsync(created);

        Hl7.Fhir.Model.Patient? current = await FhirClient.ReadAsync<Hl7.Fhir.Model.Patient>(
            $"Patient/{created.Id}/_history/2");

        Assert.NotNull(current);
        Assert.Equal("2", current.VersionId);
        Assert.Equal("Updated", current.Name.First().Family);
    }

    [Fact]
    public async Task VRead_UnknownVersionId_Returns404()
    {
        Hl7.Fhir.Model.Patient created = await CreatePatientAsync();

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => FhirClient.ReadAsync<Hl7.Fhir.Model.Patient>($"Patient/{created.Id}/_history/99999"));

        Assert.Equal(HttpStatusCode.NotFound, ex.Status);
    }

    [Fact]
    public async Task VRead_DeletedVersion_Returns410Gone()
    {
        // Create (v1) then delete (v2). vreading v2 — the delete marker — must return 410 Gone.
        Hl7.Fhir.Model.Patient created = await CreatePatientAsync();
        await FhirClient.DeleteAsync(created);

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => FhirClient.ReadAsync<Hl7.Fhir.Model.Patient>($"Patient/{created.Id}/_history/2"));

        Assert.Equal(HttpStatusCode.Gone, ex.Status);
    }

    // === history: GET [base]/Patient/[id]/_history ===

    [Fact]
    public async Task History_AfterCreate_ContainsSingleEntry()
    {
        Hl7.Fhir.Model.Patient created = await CreatePatientAsync();

        Hl7.Fhir.Model.Bundle? bundle = await FhirClient.HistoryAsync($"Patient/{created.Id}");

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task History_AfterUpdate_ContainsTwoEntries()
    {
        Hl7.Fhir.Model.Patient created = await CreatePatientAsync("Original");
        created.Name.First().Family = "Updated";
        await FhirClient.UpdateAsync(created);

        Hl7.Fhir.Model.Bundle? bundle = await FhirClient.HistoryAsync($"Patient/{created.Id}");

        Assert.NotNull(bundle);
        Assert.Equal(2, bundle.Entry.Count);
    }

    [Fact]
    public async Task History_EntriesAreSortedNewestFirst()
    {
        // FHIR spec: history bundle entries are sorted with oldest versions last (newest first).
        Hl7.Fhir.Model.Patient created = await CreatePatientAsync("Original");
        created.Name.First().Family = "Updated";
        await FhirClient.UpdateAsync(created);

        Hl7.Fhir.Model.Bundle? bundle = await FhirClient.HistoryAsync($"Patient/{created.Id}");

        Assert.NotNull(bundle);
        Assert.Equal(2, bundle.Entry.Count);
        var newest = Assert.IsType<Hl7.Fhir.Model.Patient>(bundle.Entry[0].Resource);
        Assert.Equal("2", newest.VersionId);
    }

    [Fact]
    public async Task History_AfterDelete_ContainsTwoEntries()
    {
        Hl7.Fhir.Model.Patient created = await CreatePatientAsync();
        await FhirClient.DeleteAsync(created);

        Hl7.Fhir.Model.Bundle? bundle = await FhirClient.HistoryAsync($"Patient/{created.Id}");

        Assert.NotNull(bundle);
        Assert.Equal(2, bundle.Entry.Count);
    }

    [Fact]
    public async Task History_AfterDelete_LatestEntryHasNoResource()
    {
        // A DELETE history entry omits the resource body per the FHIR spec — only request element is present.
        Hl7.Fhir.Model.Patient created = await CreatePatientAsync();
        await FhirClient.DeleteAsync(created);

        Hl7.Fhir.Model.Bundle? bundle = await FhirClient.HistoryAsync($"Patient/{created.Id}");

        Assert.NotNull(bundle);
        Assert.Null(bundle.Entry[0].Resource);
    }

    // === helpers ===

    private async Task<Hl7.Fhir.Model.Patient> CreatePatientAsync(string? familyName = null)
    {
        Hl7.Fhir.Model.Patient? created = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: familyName));
        Assert.NotNull(created);
        return created;
    }
}

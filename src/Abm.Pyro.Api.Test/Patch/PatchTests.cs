using System.Net;
using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.AspNetCore.Mvc.Testing;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Patch;

/// <summary>
/// Full-stack integration tests for the FHIR R4 FHIRPath Patch interaction.
///
/// Key invariant: PATCH never creates — 404 when the target does not exist,
/// unlike PUT which upserts.
///
/// Direct PATCH  → FhirClient.PatchAsync(new Uri("Patient/{id}", Relative), params)
/// Conditional   → FhirClient.PatchAsync(new Uri("Patient?{criteria}", Relative), params)
/// Error cases   → FhirOperationException { Status }
/// </summary>
public class PatchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string MrnSystem = "http://example.org/fhir/mrn";

    // ── patch body builder ────────────────────────────────────────────────────

    private static Parameters MakeOp(
        string    type,
        string    path,
        string?   name        = null,
        DataType? value       = null,
        int?      index       = null,
        int?      source      = null,
        int?      destination = null)
    {
        var op = new Parameters.ParameterComponent
        {
            Name = "operation",
            Part =
            [
                new() { Name = "type", Value = new Code(type) },
                new() { Name = "path", Value = new FhirString(path) }
            ]
        };
        if (name        is not null) op.Part.Add(new() { Name = "name",        Value = new FhirString(name) });
        if (value       is not null) op.Part.Add(new() { Name = "value",       Value = value });
        if (index       is not null) op.Part.Add(new() { Name = "index",       Value = new Integer(index) });
        if (source      is not null) op.Part.Add(new() { Name = "source",      Value = new Integer(source) });
        if (destination is not null) op.Part.Add(new() { Name = "destination", Value = new Integer(destination) });

        var p = new Parameters();
        p.Parameter.Add(op);
        return p;
    }

    // ── FhirClient helpers ────────────────────────────────────────────────────

    /// Direct PATCH by resource ID via FhirClient.
    private Task<Resource?> PatchAsync(string resourceType, string id, Parameters patch)
        => FhirClient.PatchAsync(new Uri($"{resourceType}/{id}", UriKind.Relative), patch);

    /// Conditional PATCH via FhirClient.ConditionalPatchAsync<TResource>(SearchParams, Parameters).
    /// criteria is a "key=value[&key=value]" string parsed into SearchParams.
    private Task<TResource?> ConditionalPatchAsync<TResource>(string searchCriteria, Parameters patch)
        where TResource : Resource
    {
        var condition = new SearchParams();
        foreach (string pair in searchCriteria.Split('&'))
        {
            string[] kv = pair.Split('=', 2);
            if (kv.Length == 2)
                condition.Add(kv[0], Uri.UnescapeDataString(kv[1]));
        }
        return FhirClient.ConditionalPatchAsync<TResource>(condition, patch);
    }

    /// Creates a fresh FhirClient backed by the test server that adds an
    /// If-Match header to every request — used for optimistic-concurrency tests.
    private FhirClient CreateFhirClientWithIfMatch(string etag)
    {
        HttpClient httpClient = Fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", etag);
        var tenantBase = new Uri(httpClient.BaseAddress!, "pyro/");
        return new FhirClient(tenantBase, httpClient, new FhirClientSettings { PreferredFormat = ResourceFormat.Json });
    }

    // ── setup helpers ─────────────────────────────────────────────────────────

    private async Task<Patient> CreatePatientAsync(string? familyName = null)
    {
        Patient? created = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: familyName));
        Assert.NotNull(created);
        return created;
    }

    private async Task<Patient> CreatePatientWithIdentifierAsync(string mrn, string? familyName = null)
    {
        Patient patient = PatientBuilder.Build(familyName: familyName);
        patient.Identifier.Add(new Identifier(MrnSystem, mrn));
        Patient? created = await FhirClient.CreateAsync(patient);
        Assert.NotNull(created);
        return created;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Direct PATCH — by resource ID
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Patch_ExistingPatient_Returns200()
    {
        Patient created = await CreatePatientAsync("Smith");
        Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("Jones"));

        Resource? result = await PatchAsync("Patient", created.Id!, patch);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Patch_ExistingPatient_PersistsChange_ConfirmedBySubsequentRead()
    {
        Patient created = await CreatePatientAsync("Smith");
        Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("Jones"));

        await PatchAsync("Patient", created.Id!, patch);
        Patient? readBack = await FhirClient.ReadAsync<Patient>($"Patient/{created.Id}");

        Assert.NotNull(readBack);
        Assert.Equal("Jones", readBack.Name.First().Family);
    }

    [Fact]
    public async Task Patch_ExistingPatient_IncrementsVersionId()
    {
        Patient created = await CreatePatientAsync(); // version 1

        Resource? result = await PatchAsync("Patient", created.Id!,
            MakeOp("replace", "Patient.name[0].family", value: new FhirString("Updated")));

        Patient patched = Assert.IsType<Patient>(result);
        Assert.Equal("2", patched.Meta?.VersionId);
    }

    [Fact]
    public async Task Patch_ExistingPatient_ResponseContainsUpdatedResource()
    {
        Patient created = await CreatePatientAsync("Before");

        Resource? result = await PatchAsync("Patient", created.Id!,
            MakeOp("replace", "Patient.name[0].family", value: new FhirString("After")));

        Patient patched = Assert.IsType<Patient>(result);
        Assert.Equal(created.Id, patched.Id);
        Assert.Equal("After",    patched.Name.First().Family);
        Assert.NotNull(patched.Meta?.LastUpdated);
    }

    [Fact]
    public async Task Patch_ExistingPatient_AddOperation_AppendsNewName()
    {
        Patient created = await CreatePatientAsync(); // starts with one name
        Parameters patch = MakeOp("add", "Patient", name: "name", value: new HumanName { Family = "Alias" });

        await PatchAsync("Patient", created.Id!, patch);
        Patient? readBack = await FhirClient.ReadAsync<Patient>($"Patient/{created.Id}");

        Assert.NotNull(readBack);
        Assert.Equal(2, readBack.Name.Count);
        Assert.Contains(readBack.Name, n => n.Family == "Alias");
    }

    [Fact]
    public async Task Patch_ExistingPatient_DeleteOperation_RemovesField()
    {
        Patient created = await CreatePatientAsync(); // PatientBuilder sets BirthDate = "1990-01-15"
        Parameters patch = MakeOp("delete", "Patient.birthDate");

        await PatchAsync("Patient", created.Id!, patch);
        Patient? readBack = await FhirClient.ReadAsync<Patient>($"Patient/{created.Id}");

        Assert.NotNull(readBack);
        Assert.Null(readBack.BirthDate);
    }

    [Fact]
    public async Task Patch_ExistingPatient_MultipleOperations_AllApplied()
    {
        Patient created = await CreatePatientAsync("Duck");

        var patch = new Parameters();
        patch.Parameter.Add(new Parameters.ParameterComponent
        {
            Name = "operation",
            Part =
            [
                new() { Name = "type",  Value = new Code("replace") },
                new() { Name = "path",  Value = new FhirString("Patient.name[0].family") },
                new() { Name = "value", Value = new FhirString("Quack") }
            ]
        });
        patch.Parameter.Add(new Parameters.ParameterComponent
        {
            Name = "operation",
            Part =
            [
                new() { Name = "type",  Value = new Code("replace") },
                new() { Name = "path",  Value = new FhirString("Patient.gender") },
                new() { Name = "value", Value = new Code("male") }
            ]
        });

        await PatchAsync("Patient", created.Id!, patch);
        Patient? readBack = await FhirClient.ReadAsync<Patient>($"Patient/{created.Id}");

        Assert.NotNull(readBack);
        Assert.Equal("Quack",                   readBack.Name.First().Family);
        Assert.Equal(AdministrativeGender.Male, readBack.Gender);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH never creates (critical invariant — unlike PUT which upserts)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Patch_NonExistentPatient_Returns404_NotCreated()
    {
        Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("Ghost"));

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => PatchAsync("Patient", "id-does-not-exist-99999", patch));

        Assert.Equal(HttpStatusCode.NotFound, ex.Status);
    }

    [Fact]
    public async Task Patch_DeletedPatient_Returns404()
    {
        Patient created = await CreatePatientAsync();
        await FhirClient.DeleteAsync(created);

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => PatchAsync("Patient", created.Id!,
                MakeOp("replace", "Patient.name[0].family", value: new FhirString("Ghost"))));

        Assert.Equal(HttpStatusCode.NotFound, ex.Status);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Optimistic concurrency (If-Match)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Patch_IfMatch_CorrectVersion_Succeeds()
    {
        Patient created = await CreatePatientAsync(); // version 1
        FhirClient clientWithIfMatch = CreateFhirClientWithIfMatch("W/\"1\"");
        Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("Versioned"));

        Resource? result = await clientWithIfMatch.PatchAsync(
            new Uri($"Patient/{created.Id}", UriKind.Relative), patch);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Patch_IfMatch_StaleVersion_Returns412()
    {
        Patient created = await CreatePatientAsync(); // version 1
        FhirClient clientWithIfMatch = CreateFhirClientWithIfMatch("W/\"99\"");
        Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("Versioned"));

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => clientWithIfMatch.PatchAsync(
                new Uri($"Patient/{created.Id}", UriKind.Relative), patch));

        Assert.Equal(HttpStatusCode.PreconditionFailed, ex.Status);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Validation / bad-request errors
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Patch_EmptyOperations_Returns400()
    {
        Patient created = await CreatePatientAsync();

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => PatchAsync("Patient", created.Id!, new Parameters()));

        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
    }

    [Fact]
    public async Task Patch_PathNotFound_Returns400()
    {
        Patient created = await CreatePatientAsync();

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => PatchAsync("Patient", created.Id!,
                MakeOp("delete", "Patient.nonExistentElement")));

        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
    }

    [Fact]
    public async Task Patch_UnknownOperationType_Returns400()
    {
        Patient created = await CreatePatientAsync();

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => PatchAsync("Patient", created.Id!,
                MakeOp("explode", "Patient.name[0].family")));

        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
    }

    [Fact]
    public async Task Patch_IndexOutOfRange_Returns400()
    {
        Patient created = await CreatePatientAsync(); // one name at index [0]

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => PatchAsync("Patient", created.Id!, MakeOp("delete", "Patient.name[99]")));

        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Conditional PATCH (PATCH /pyro/Patient?{criteria})
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConditionalPatch_OneMatch_Returns200()
    {
        const string mrn = "cpatch-one-1";
        await CreatePatientWithIdentifierAsync(mrn, "Smith");
        Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("Jones"));

        Resource? result = await ConditionalPatchAsync<Patient>($"identifier={MrnSystem}|{mrn}", patch);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ConditionalPatch_OneMatch_PersistsChange_ConfirmedBySubsequentRead()
    {
        const string mrn = "cpatch-one-2";
        Patient created = await CreatePatientWithIdentifierAsync(mrn, "Smith");
        Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("Jones"));

        await ConditionalPatchAsync<Patient>($"identifier={MrnSystem}|{mrn}", patch);
        Patient? readBack = await FhirClient.ReadAsync<Patient>($"Patient/{created.Id}");

        Assert.NotNull(readBack);
        Assert.Equal("Jones", readBack.Name.First().Family);
    }

    [Fact]
    public async Task ConditionalPatch_NoMatch_Returns404()
    {
        Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("Jones"));

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => ConditionalPatchAsync<Patient>($"identifier={MrnSystem}|no-such-patient-99999", patch));

        Assert.Equal(HttpStatusCode.NotFound, ex.Status);
    }

    [Fact]
    public async Task ConditionalPatch_MultipleMatches_Returns412()
    {
        const string mrn = "cpatch-multi-1";
        await CreatePatientWithIdentifierAsync(mrn, "Alpha");
        await CreatePatientWithIdentifierAsync(mrn, "Beta");
        Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("Gamma"));

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => ConditionalPatchAsync<Patient>($"identifier={MrnSystem}|{mrn}", patch));

        Assert.Equal(HttpStatusCode.PreconditionFailed, ex.Status);
    }
}

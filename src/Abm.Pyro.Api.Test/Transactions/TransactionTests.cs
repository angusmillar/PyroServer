using System;
using System.Linq;
using System.Net;
using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Transactions;

/// <summary>
/// Full-stack integration tests for the FHIR R4 <c>transaction</c> interaction
/// (a <see cref="Bundle"/> with <c>type = transaction</c> POSTed to the base endpoint).
/// Ref: https://hl7.org/fhir/R4/http.html#transaction
///
/// These tests assert both the returned <c>transaction-response</c> bundle AND the
/// independently observable side effects (follow-up reads / searches), since a transaction
/// is only meaningful if the committed state actually changed.
/// </summary>
public class TransactionTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string MrnSystem = "http://example.org/fhir/mrn";

    // ------------------------------------------------------------------
    // Headline test: Create + Update + Delete in a single transaction.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Transaction_CreateUpdateDelete_AllSucceedAtomically()
    {
        // Arrange: seed one patient to update and one to delete.
        Patient toUpdate = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Original"))
                           ?? throw new InvalidOperationException("Seed create (toUpdate) returned null");
        Patient toDelete = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Doomed"))
                           ?? throw new InvalidOperationException("Seed create (toDelete) returned null");

        Patient updated = PatientBuilder.Build(id: toUpdate.Id, familyName: "Updated");

        Bundle transaction = TransactionBundle(
            PostEntry(PatientBuilder.Build(familyName: "Created")),
            PutEntry(updated, $"Patient/{toUpdate.Id}"),
            DeleteEntry($"Patient/{toDelete.Id}", fullUrl: $"urn:uuid:{Guid.NewGuid()}"));

        // Act
        Bundle? response = await FhirClient.TransactionAsync(transaction);

        // Assert: response bundle shape and per-entry statuses (order is preserved).
        Assert.NotNull(response);
        Assert.Equal(Bundle.BundleType.TransactionResponse, response.Type);
        Assert.Equal(3, response.Entry.Count);
        Assert.StartsWith("201", response.Entry[0].Response.Status); // POST -> Created
        Assert.StartsWith("200", response.Entry[1].Response.Status); // PUT  -> OK
        Assert.StartsWith("204", response.Entry[2].Response.Status); // DELETE -> No Content

        // Assert side effects via independent reads.
        var createdId = response.Entry[0].Resource.Id;
        Patient createdReadBack = await FhirClient.ReadAsync<Patient>($"Patient/{createdId}")
                                  ?? throw new InvalidOperationException("Created patient not found");
        Assert.Equal("Created", createdReadBack.Name.First().Family);

        Patient updatedReadBack = await FhirClient.ReadAsync<Patient>($"Patient/{toUpdate.Id}")
                                  ?? throw new InvalidOperationException("Updated patient not found");
        Assert.Equal("Updated", updatedReadBack.Name.First().Family);
        Assert.Equal("2", updatedReadBack.Meta.VersionId); // create (v1) then update (v2)

        FhirOperationException deletedReadEx = await Assert.ThrowsAsync<FhirOperationException>(
            () => FhirClient.ReadAsync<Patient>($"Patient/{toDelete.Id}"));
        Assert.Equal(HttpStatusCode.Gone, deletedReadEx.Status);
    }

    // ------------------------------------------------------------------
    // Simple POST-only transaction.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Transaction_PostSingleResource_CreatesAndPersists()
    {
        Bundle transaction = TransactionBundle(
            PostEntry(PatientBuilder.Build(familyName: "Solo")));

        Bundle? response = await FhirClient.TransactionAsync(transaction);

        Assert.NotNull(response);
        Assert.Single(response.Entry);
        Assert.StartsWith("201", response.Entry[0].Response.Status);

        var createdId = response.Entry[0].Resource.Id;
        Assert.False(string.IsNullOrWhiteSpace(createdId));

        Patient readBack = await FhirClient.ReadAsync<Patient>($"Patient/{createdId}")
                           ?? throw new InvalidOperationException("Created patient not found");
        Assert.Equal("Solo", readBack.Name.First().Family);
    }

    // ------------------------------------------------------------------
    // Internal references: urn:uuid placeholder resolved to the assigned id.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Transaction_InternalUuidReference_ResolvesBetweenEntries()
    {
        string patientUrn = $"urn:uuid:{Guid.NewGuid()}";

        Observation observation = ObservationBuilder.Build();
        observation.Subject = new ResourceReference(patientUrn);

        Bundle transaction = TransactionBundle(
            PostEntry(PatientBuilder.Build(familyName: "Referenced"), fullUrl: patientUrn),
            PostEntry(observation));

        Bundle? response = await FhirClient.TransactionAsync(transaction);

        Assert.NotNull(response);
        Assert.Equal(2, response.Entry.Count);
        Assert.StartsWith("201", response.Entry[0].Response.Status);
        Assert.StartsWith("201", response.Entry[1].Response.Status);

        var patientId = response.Entry[0].Resource.Id;
        var responseObservation = Assert.IsType<Observation>(response.Entry[1].Resource);

        // The urn:uuid placeholder must have been rewritten to a concrete Patient reference.
        Assert.Contains($"Patient/{patientId}", responseObservation.Subject.Reference);

        // Confirm the persisted observation references the created patient.
        Observation readBack = await FhirClient.ReadAsync<Observation>($"Observation/{responseObservation.Id}")
                               ?? throw new InvalidOperationException("Created observation not found");
        Assert.Contains(patientId, readBack.Subject.Reference);
    }

    // ------------------------------------------------------------------
    // Conditional create (If-None-Exist).
    // ------------------------------------------------------------------

    [Fact]
    public async Task Transaction_ConditionalCreate_NoMatch_CreatesResource()
    {
        const string mrn = "cc-nomatch";

        Bundle transaction = TransactionBundle(
            PostEntry(PatientWithIdentifier(mrn, family: "ConditionallyCreated"),
                ifNoneExist: $"identifier={MrnSystem}|{mrn}"));

        Bundle? response = await FhirClient.TransactionAsync(transaction);

        Assert.NotNull(response);
        Assert.StartsWith("201", response.Entry[0].Response.Status);

        Bundle? search = await FhirClient.SearchAsync<Patient>(new[] { $"identifier={MrnSystem}|{mrn}" });
        Assert.NotNull(search);
        Assert.Single(search.Entry);
    }

    [Fact]
    public async Task Transaction_ConditionalCreate_ExistingMatch_IgnoresAndReturns200()
    {
        const string mrn = "cc-match";
        await FhirClient.CreateAsync(PatientWithIdentifier(mrn, family: "PreExisting"));

        Bundle transaction = TransactionBundle(
            PostEntry(PatientWithIdentifier(mrn, family: "ShouldBeIgnored"),
                ifNoneExist: $"identifier={MrnSystem}|{mrn}"));

        Bundle? response = await FhirClient.TransactionAsync(transaction);

        // One match => the server ignores the POST and returns 200 OK (no new resource).
        Assert.NotNull(response);
        Assert.StartsWith("200", response.Entry[0].Response.Status);

        Bundle? search = await FhirClient.SearchAsync<Patient>(new[] { $"identifier={MrnSystem}|{mrn}" });
        Assert.NotNull(search);
        Assert.Single(search.Entry); // still exactly one - no duplicate created
    }

    // ------------------------------------------------------------------
    // Conditional update (PUT with search criteria).
    // ------------------------------------------------------------------

    [Fact]
    public async Task Transaction_ConditionalUpdate_OneMatch_UpdatesExisting()
    {
        const string mrn = "cu-1";
        Patient existing = await FhirClient.CreateAsync(PatientWithIdentifier(mrn, family: "Before"))
                           ?? throw new InvalidOperationException("Seed create returned null");

        // No id on the resource - the match is resolved purely from the search criteria.
        Patient update = PatientWithIdentifier(mrn, family: "After");

        Bundle transaction = TransactionBundle(
            PutEntry(update, $"Patient?identifier={MrnSystem}|{mrn}"));

        Bundle? response = await FhirClient.TransactionAsync(transaction);

        Assert.NotNull(response);
        Assert.StartsWith("200", response.Entry[0].Response.Status);
        Assert.Equal(existing.Id, response.Entry[0].Resource.Id); // updated the matched resource

        Patient readBack = await FhirClient.ReadAsync<Patient>($"Patient/{existing.Id}")
                           ?? throw new InvalidOperationException("Updated patient not found");
        Assert.Equal("After", readBack.Name.First().Family);
        Assert.Equal("2", readBack.Meta.VersionId);
    }

    // ------------------------------------------------------------------
    // Conditional delete (DELETE with search criteria).
    // ------------------------------------------------------------------

    [Fact]
    public async Task Transaction_ConditionalDelete_OneMatch_DeletesResource()
    {
        const string mrn = "cd-1";
        Patient existing = await FhirClient.CreateAsync(PatientWithIdentifier(mrn, family: "ToBeConditionallyDeleted"))
                           ?? throw new InvalidOperationException("Seed create returned null");

        Bundle transaction = TransactionBundle(
            DeleteEntry($"Patient?identifier={MrnSystem}|{mrn}"));

        Bundle? response = await FhirClient.TransactionAsync(transaction);

        Assert.NotNull(response);
        Assert.StartsWith("204", response.Entry[0].Response.Status);

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => FhirClient.ReadAsync<Patient>($"Patient/{existing.Id}"));
        Assert.Equal(HttpStatusCode.Gone, ex.Status);

        Bundle? search = await FhirClient.SearchAsync<Patient>(new[] { $"identifier={MrnSystem}|{mrn}" });
        Assert.NotNull(search);
        Assert.Empty(search.Entry);
    }

    // ------------------------------------------------------------------
    // Conditional reference: 'Patient?identifier=...' resolved to an existing resource.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Transaction_ConditionalReference_ResolvesToExistingResource()
    {
        const string mrn = "ref-1";
        Patient existing = await FhirClient.CreateAsync(PatientWithIdentifier(mrn, family: "ReferenceTarget"))
                           ?? throw new InvalidOperationException("Seed create returned null");

        Observation observation = ObservationBuilder.Build();
        observation.Subject = new ResourceReference($"Patient?identifier={MrnSystem}|{mrn}");

        Bundle transaction = TransactionBundle(PostEntry(observation));

        Bundle? response = await FhirClient.TransactionAsync(transaction);

        Assert.NotNull(response);
        Assert.StartsWith("201", response.Entry[0].Response.Status);

        var responseObservation = Assert.IsType<Observation>(response.Entry[0].Resource);
        Assert.Contains($"Patient/{existing.Id}", responseObservation.Subject.Reference);

        Observation readBack = await FhirClient.ReadAsync<Observation>($"Observation/{responseObservation.Id}")
                               ?? throw new InvalidOperationException("Created observation not found");
        Assert.Contains($"Patient/{existing.Id}", readBack.Subject.Reference);
    }

    // ------------------------------------------------------------------
    // GET interactions inside a transaction.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Transaction_GetEntry_ReturnsResourceInResponseBundle()
    {
        Patient existing = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Readable"))
                           ?? throw new InvalidOperationException("Seed create returned null");

        Bundle transaction = TransactionBundle(GetEntry($"Patient/{existing.Id}"));

        Bundle? response = await FhirClient.TransactionAsync(transaction);

        Assert.NotNull(response);
        Assert.Single(response.Entry);
        Assert.StartsWith("200", response.Entry[0].Response.Status);

        var fetched = Assert.IsType<Patient>(response.Entry[0].Resource);
        Assert.Equal(existing.Id, fetched.Id);
        Assert.Equal("Readable", fetched.Name.First().Family);
    }

    // ------------------------------------------------------------------
    // Atomicity: a single failing entry rolls back the whole transaction.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Transaction_FailedEntry_RollsBackEntireTransaction()
    {
        const string mrn = "rollback-1";

        // Entry 1 is a perfectly valid create; entry 2 is an invalid PUT whose resource id
        // does not match its request.url id. The transaction must fail atomically, leaving
        // NO trace of the otherwise-valid create.
        Patient mismatch = PatientBuilder.Build(id: "resource-id-aaa", familyName: "Mismatch");

        Bundle transaction = TransactionBundle(
            PostEntry(PatientWithIdentifier(mrn, family: "RollbackVictim")),
            PutEntry(mismatch, "Patient/request-url-id-bbb"));

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => FhirClient.TransactionAsync(transaction));
        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);

        // The valid create from entry 1 must not have been committed.
        Bundle? search = await FhirClient.SearchAsync<Patient>(new[] { $"identifier={MrnSystem}|{mrn}" });
        Assert.NotNull(search);
        Assert.Empty(search.Entry);
    }

    [Fact]
    public async Task Transaction_DuplicateFullUrl_IsRejected()
    {
        string sharedFullUrl = $"urn:uuid:{Guid.NewGuid()}";

        Bundle transaction = TransactionBundle(
            PostEntry(PatientBuilder.Build(familyName: "First"), fullUrl: sharedFullUrl),
            PostEntry(PatientBuilder.Build(familyName: "Second"), fullUrl: sharedFullUrl));

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => FhirClient.TransactionAsync(transaction));
        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
    }

    // ------------------------------------------------------------------
    // PATCH inside a transaction Bundle.
    // Ref: https://hl7.org/fhir/R4/fhirpatch.html
    // Key invariant carried over from the standalone PATCH endpoint: PATCH never creates.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Transaction_DirectPatch_ExistingPatient_UpdatesAndPersists()
    {
        Patient existing = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "PreImage"))
                           ?? throw new InvalidOperationException("Seed create returned null");

        Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("PostImage"));

        Bundle transaction = TransactionBundle(
            PatchEntry($"Patient/{existing.Id}", patch));

        Bundle? response = await FhirClient.TransactionAsync(transaction);

        Assert.NotNull(response);
        Assert.Single(response.Entry);
        Assert.StartsWith("200", response.Entry[0].Response.Status);

        var patchedPatient = Assert.IsType<Patient>(response.Entry[0].Resource);
        Assert.Equal("PostImage", patchedPatient.Name.First().Family);
        Assert.Equal("2", patchedPatient.Meta.VersionId);

        Patient readBack = await FhirClient.ReadAsync<Patient>($"Patient/{existing.Id}")
                           ?? throw new InvalidOperationException("Patched patient not found");
        Assert.Equal("PostImage", readBack.Name.First().Family);
        Assert.Equal("2", readBack.Meta.VersionId);
    }

    [Fact]
    public async Task Transaction_ConditionalPatch_OneMatch_UpdatesExisting()
    {
        const string mrn = "cp-1";
        Patient existing = await FhirClient.CreateAsync(PatientWithIdentifier(mrn, family: "Before"))
                           ?? throw new InvalidOperationException("Seed create returned null");

        Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("After"));

        Bundle transaction = TransactionBundle(
            PatchEntry($"Patient?identifier={MrnSystem}|{mrn}", patch));

        Bundle? response = await FhirClient.TransactionAsync(transaction);

        Assert.NotNull(response);
        Assert.StartsWith("200", response.Entry[0].Response.Status);
        Assert.Equal(existing.Id, response.Entry[0].Resource.Id);

        Patient readBack = await FhirClient.ReadAsync<Patient>($"Patient/{existing.Id}")
                           ?? throw new InvalidOperationException("Patched patient not found");
        Assert.Equal("After", readBack.Name.First().Family);
    }

    [Fact]
    public async Task Transaction_ConditionalPatch_ZeroMatches_RollsBackWholeTransaction()
    {
        const string mrn = "cp-zero";
        const string siblingMrn = "cp-zero-sibling";

        Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("ShouldNotApply"));

        Bundle transaction = TransactionBundle(
            PostEntry(PatientWithIdentifier(siblingMrn, family: "RollbackVictim")),
            PatchEntry($"Patient?identifier={MrnSystem}|{mrn}", patch));

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => FhirClient.TransactionAsync(transaction));
        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);

        // The sibling POST must not have been committed either - the transaction is atomic.
        Bundle? search = await FhirClient.SearchAsync<Patient>(new[] { $"identifier={MrnSystem}|{siblingMrn}" });
        Assert.NotNull(search);
        Assert.Empty(search.Entry);
    }

    [Fact]
    public async Task Transaction_ConditionalPatch_MultipleMatches_FailsTransaction()
    {
        const string mrn = "cp-multi";
        await FhirClient.CreateAsync(PatientWithIdentifier(mrn, family: "First"));
        await FhirClient.CreateAsync(PatientWithIdentifier(mrn, family: "Second"));

        Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("ShouldNotApply"));

        Bundle transaction = TransactionBundle(
            PatchEntry($"Patient?identifier={MrnSystem}|{mrn}", patch));

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => FhirClient.TransactionAsync(transaction));
        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
    }

    [Fact]
    public async Task Transaction_DirectPatch_NonExistentResource_FailsTransaction()
    {
        Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("ShouldNotApply"));

        Bundle transaction = TransactionBundle(
            PatchEntry($"Patient/{Guid.NewGuid()}", patch));

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => FhirClient.TransactionAsync(transaction));
        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
    }

    [Fact]
    public async Task Transaction_DirectPatch_DeletedResource_FailsTransaction()
    {
        Patient existing = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "ToBeDeletedThenPatched"))
                           ?? throw new InvalidOperationException("Seed create returned null");
        await FhirClient.DeleteAsync($"Patient/{existing.Id}");

        Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("ShouldNotApply"));

        Bundle transaction = TransactionBundle(
            PatchEntry($"Patient/{existing.Id}", patch));

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => FhirClient.TransactionAsync(transaction));
        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
    }

    [Fact]
    public async Task Transaction_MixedVerbBundle_AllSucceedIncludingPatch()
    {
        Patient toUpdate = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "MixedPutOriginal"))
                           ?? throw new InvalidOperationException("Seed create (toUpdate) returned null");
        Patient toPatch = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "MixedPatchOriginal"))
                          ?? throw new InvalidOperationException("Seed create (toPatch) returned null");
        Patient toDelete = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "MixedDoomed"))
                           ?? throw new InvalidOperationException("Seed create (toDelete) returned null");
        Patient toGet = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "MixedReadable"))
                        ?? throw new InvalidOperationException("Seed create (toGet) returned null");

        Patient updated = PatientBuilder.Build(id: toUpdate.Id, familyName: "MixedPutUpdated");
        Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("MixedPatchUpdated"));

        Bundle transaction = TransactionBundle(
            PostEntry(PatientBuilder.Build(familyName: "MixedCreated")),
            PutEntry(updated, $"Patient/{toUpdate.Id}"),
            PatchEntry($"Patient/{toPatch.Id}", patch),
            DeleteEntry($"Patient/{toDelete.Id}"),
            GetEntry($"Patient/{toGet.Id}"));

        Bundle? response = await FhirClient.TransactionAsync(transaction);

        Assert.NotNull(response);
        Assert.Equal(5, response.Entry.Count);
        Assert.StartsWith("201", response.Entry[0].Response.Status); // POST
        Assert.StartsWith("200", response.Entry[1].Response.Status); // PUT
        Assert.StartsWith("200", response.Entry[2].Response.Status); // PATCH
        Assert.StartsWith("204", response.Entry[3].Response.Status); // DELETE
        Assert.StartsWith("200", response.Entry[4].Response.Status); // GET

        var patchedPatient = Assert.IsType<Patient>(response.Entry[2].Resource);
        Assert.Equal("MixedPatchUpdated", patchedPatient.Name.First().Family);

        Patient patchedReadBack = await FhirClient.ReadAsync<Patient>($"Patient/{toPatch.Id}")
                                  ?? throw new InvalidOperationException("Patched patient not found");
        Assert.Equal("MixedPatchUpdated", patchedReadBack.Name.First().Family);
    }

    [Fact]
    public async Task Transaction_PatchValueReferencingSameBundlePost_ResolvesToNewId()
    {
        Observation existingObservation = await FhirClient.CreateAsync(ObservationBuilder.Build())
                                           ?? throw new InvalidOperationException("Seed create returned null");

        string patientUrn = $"urn:uuid:{Guid.NewGuid()}";
        Parameters patch = MakeOp("add", "Observation", name: "subject", value: new ResourceReference(patientUrn));

        Bundle transaction = TransactionBundle(
            PostEntry(PatientBuilder.Build(familyName: "PatchValueTarget"), fullUrl: patientUrn),
            PatchEntry($"Observation/{existingObservation.Id}", patch));

        Bundle? response = await FhirClient.TransactionAsync(transaction);

        Assert.NotNull(response);
        Assert.Equal(2, response.Entry.Count);
        Assert.StartsWith("201", response.Entry[0].Response.Status);
        Assert.StartsWith("200", response.Entry[1].Response.Status);

        var newPatientId = response.Entry[0].Resource.Id;
        var patchedObservation = Assert.IsType<Observation>(response.Entry[1].Resource);
        Assert.Contains($"Patient/{newPatientId}", patchedObservation.Subject.Reference);

        Observation readBack = await FhirClient.ReadAsync<Observation>($"Observation/{existingObservation.Id}")
                               ?? throw new InvalidOperationException("Patched observation not found");
        Assert.Contains($"Patient/{newPatientId}", readBack.Subject.Reference);
    }

    [Fact]
    public async Task Transaction_PutAndPatchOverlapOnSameTarget_FailsTransaction()
    {
        Patient existing = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "OverlapOriginal"))
                           ?? throw new InvalidOperationException("Seed create returned null");

        Patient putUpdate = PatientBuilder.Build(id: existing.Id, familyName: "OverlapViaPut");
        Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("OverlapViaPatch"));

        Bundle transaction = TransactionBundle(
            PutEntry(putUpdate, $"Patient/{existing.Id}"),
            PatchEntry($"Patient/{existing.Id}", patch));

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => FhirClient.TransactionAsync(transaction));
        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);

        // Neither the PUT nor the PATCH may have been committed.
        Patient readBack = await FhirClient.ReadAsync<Patient>($"Patient/{existing.Id}")
                           ?? throw new InvalidOperationException("Patient not found");
        Assert.Equal("OverlapOriginal", readBack.Name.First().Family);
        Assert.Equal("1", readBack.Meta.VersionId);
    }

    [Fact]
    public async Task Transaction_DirectPatch_IfMatchPreconditionFailure_FailsTransaction()
    {
        Patient existing = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "IfMatchOriginal"))
                           ?? throw new InvalidOperationException("Seed create returned null");

        Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("ShouldNotApply"));

        Bundle transaction = TransactionBundle(
            PatchEntry($"Patient/{existing.Id}", patch, ifMatch: "W/\"99\""));

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => FhirClient.TransactionAsync(transaction));
        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);

        Patient readBack = await FhirClient.ReadAsync<Patient>($"Patient/{existing.Id}")
                           ?? throw new InvalidOperationException("Patient not found");
        Assert.Equal("IfMatchOriginal", readBack.Name.First().Family);
        Assert.Equal("1", readBack.Meta.VersionId);
    }

    [Fact]
    public async Task Transaction_DirectPatch_EmptyParametersBody_FailsTransaction()
    {
        Patient existing = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "EmptyPatchOriginal"))
                           ?? throw new InvalidOperationException("Seed create returned null");

        Bundle transaction = TransactionBundle(
            PatchEntry($"Patient/{existing.Id}", new Parameters()));

        FhirOperationException ex = await Assert.ThrowsAsync<FhirOperationException>(
            () => FhirClient.TransactionAsync(transaction));
        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);

        Patient readBack = await FhirClient.ReadAsync<Patient>($"Patient/{existing.Id}")
                           ?? throw new InvalidOperationException("Patient not found");
        Assert.Equal("EmptyPatchOriginal", readBack.Name.First().Family);
        Assert.Equal("1", readBack.Meta.VersionId);
    }

    // ------------------------------------------------------------------
    // Response bundle metadata population.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Transaction_ResponseBundle_PopulatesEntryResponseMetadata()
    {
        Bundle transaction = TransactionBundle(
            PostEntry(PatientBuilder.Build(familyName: "MetadataCheck")));

        Bundle? response = await FhirClient.TransactionAsync(transaction);

        Assert.NotNull(response);
        Assert.Equal(Bundle.BundleType.TransactionResponse, response.Type);

        Bundle.EntryComponent entry = response.Entry[0];
        Assert.NotNull(entry.Response);
        Assert.StartsWith("201", entry.Response.Status);
        Assert.StartsWith("W/", entry.Response.Etag);           // weak ETag, e.g. W/"1"
        Assert.Contains("Patient/", entry.Response.Location);   // location of the created resource
        Assert.Null(entry.Request);                             // request element is cleared on the response
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static Bundle TransactionBundle(params Bundle.EntryComponent[] entries)
    {
        var bundle = new Bundle { Type = Bundle.BundleType.Transaction };
        bundle.Entry.AddRange(entries);
        return bundle;
    }

    private static Bundle.EntryComponent PostEntry(Resource resource, string? fullUrl = null, string? ifNoneExist = null)
    {
        return new Bundle.EntryComponent
        {
            FullUrl = fullUrl ?? $"urn:uuid:{Guid.NewGuid()}",
            Resource = resource,
            Request = new Bundle.RequestComponent
            {
                Method = Bundle.HTTPVerb.POST,
                Url = resource.TypeName,
                IfNoneExist = ifNoneExist
            }
        };
    }

    private static Bundle.EntryComponent PutEntry(Resource resource, string requestUrl)
    {
        return new Bundle.EntryComponent
        {
            FullUrl = $"urn:uuid:{Guid.NewGuid()}",
            Resource = resource,
            Request = new Bundle.RequestComponent
            {
                Method = Bundle.HTTPVerb.PUT,
                Url = requestUrl
            }
        };
    }

    private static Bundle.EntryComponent DeleteEntry(string requestUrl, string? fullUrl = null)
    {
        return new Bundle.EntryComponent
        {
            FullUrl = fullUrl ?? null,
            Request = new Bundle.RequestComponent
            {
                Method = Bundle.HTTPVerb.DELETE,
                Url = requestUrl
            }
        };
    }

    private static Bundle.EntryComponent GetEntry(string requestUrl)
    {
        return new Bundle.EntryComponent
        {
            Request = new Bundle.RequestComponent
            {
                Method = Bundle.HTTPVerb.GET,
                Url = requestUrl
            }
        };
    }

    private static Bundle.EntryComponent PatchEntry(string requestUrl, Parameters patchParameters, string? fullUrl = null, string? ifMatch = null)
    {
        return new Bundle.EntryComponent
        {
            FullUrl = fullUrl ?? $"urn:uuid:{Guid.NewGuid()}",
            Resource = patchParameters,
            Request = new Bundle.RequestComponent
            {
                Method = Bundle.HTTPVerb.PATCH,
                Url = requestUrl,
                IfMatch = ifMatch
            }
        };
    }

    // Copied from Patch/PatchTests.cs's patch-body builder for use in transaction-Bundle PATCH entries.
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

    private static Patient PatientWithIdentifier(string mrn, string? family = null)
    {
        Patient patient = PatientBuilder.Build(familyName: family);
        patient.Identifier.Add(new Identifier(MrnSystem, mrn));
        return patient;
    }
}

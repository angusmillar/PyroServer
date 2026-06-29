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

    private static Patient PatientWithIdentifier(string mrn, string? family = null)
    {
        Patient patient = PatientBuilder.Build(familyName: family);
        patient.Identifier.Add(new Identifier(MrnSystem, mrn));
        return patient;
    }
}

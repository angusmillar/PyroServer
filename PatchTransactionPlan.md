# PatchTransactionPlan — FHIR PATCH support in Bundle Transactions

> **Execution note:** this plan was authored in a prior planning session (2026-07-12) after a
> full investigation of the codebase. It is intended to be executed by an AI agent in a later
> session. All design decisions marked "confirmed with the user" are settled — do not re-ask.

## Context

Pyro already supports the FHIR R4 FHIRPath PATCH interaction as standalone HTTP endpoints:
direct PATCH (`FhirPatchHandler`) and conditional PATCH (`FhirConditionalPatchHandler`), both
delegating patch application to `FhirPathPatchService`. However, a `PATCH` entry inside a FHIR
**transaction Bundle** is currently **silently ignored**: every per-verb transaction service
self-filters on its own verb (`GetPutEntry`, `GetPostEntry`, …), no service claims
`Bundle.HTTPVerb.PATCH`, no validation rejects it, and the entry comes back in the
transaction-response bundle with no `response` component and its `Parameters` body untouched.
That is a silent data-integrity hole — a client believes its patch was applied.

The goal: support PATCH (direct `ResourceType/id` and conditional `ResourceType?criteria`)
inside the transaction workflow by **reusing the existing `IFhirPatchHandler`**, following the
exact pattern `FhirTransactionPutService` already uses to reuse `IFhirUpdateHandler`.
`FhirTransactionPutService` is renamed **`FhirTransactionPutAndPatchService`** because the FHIR
R4 transaction processing rules place PUT and PATCH together in the same processing step
(step 3), interleaved in bundle-entry order.

**Normative source references (read before implementing):**
- FHIR R4 transaction interaction: https://hl7.org/fhir/R4/http.html#transaction
- FHIR R4 transaction processing rules (ordering, identity overlap, conditional references):
  https://hl7.org/fhir/R4/http.html#trules
- FHIRPath Patch: https://hl7.org/fhir/R4/fhirpatch.html

### Decisions already confirmed with the user (do not re-ask)

1. **Fix the pre-existing endpoint-policy bug in the PUT paths** while refactoring: today both
   the direct-PUT and conditional-PUT pre-process branches check `AllowConditionalCreate`.
   Correct to: direct PUT → `AllowUpdate`; conditional PUT → `AllowConditionalUpdate`.
   New PATCH paths: direct PATCH → `AllowPatch`; conditional PATCH → `AllowConditionalPatch`.
2. **Resolved-identity overlap detection for PUT/PATCH entries only**: if two PUT/PATCH entries
   in one bundle resolve to the same `ResourceType/id`, the transaction fails (per the FHIR
   trules "SHALL fail" rule). Cross-verb overlap with DELETE/POST is out of scope.
3. **Test coverage: integration tests only** (in `Abm.Pyro.Api.Test`). No new unit-test project
   work — there is no existing unit-test precedent for the transaction services.

## Key facts about the current code (verified)

| Fact | Where |
|---|---|
| Orchestrator calls `PreProcessPuts` then, at commit step 3, `ProcessPuts`. Step-3 comment already says "Process any PUT or PATCH interactions". | `src/Abm.Pyro.Application/FhirBundleService/FhirTransactionService.cs` (~lines 86–100, 145–157) |
| Per-verb filter: `GetPutEntry` returns null unless `entry.Request?.Method is Bundle.HTTPVerb.PUT`. | `FhirTransactionPutService.cs:406-414` |
| Pre-process resolves target ids into `BundleEntryTransactionMetaData.ResourceUpdateInfo` (keyed by `entry.FullUrl` in a shared dictionary) so `FhirTransactionService.UpdateResourceReferences` can rewrite references **before** commit. | `FhirTransactionPutService.cs:40-144`, `src/Abm.Pyro.Domain/FhirBundleService/BundleEntryTransactionMetaData.cs`, `ResourceUpdateInfo.cs` |
| Commit phase calls the ordinary handler in-process, passing the pre-resolved projection to avoid a re-read: `fhirUpdateHandler.Handle(tenant, requestId, resourceId, resource, headers, cancellationToken, previousResourceStore)`. | `FhirTransactionPutService.cs:146-197` |
| `IFhirPatchHandler.Handle(tenant, requestId, resourceId, resourceName, Parameters patchParameters, headers, cancellationToken, ResourceStoreUpdateProjection? previousResourceStore = null)` already exists with the identical shape — purpose-built for this reuse. | `src/Abm.Pyro.Application/FhirHandler/IFhirPatchHandler.cs`, `FhirPatchHandler.cs:54-77` |
| `FhirPatchHandler` internally: validates (`PatchRequestValidator` → `AllowPatch` policy + body-is-`Parameters`), returns **404 when target missing or deleted (PATCH never creates)**, honours `If-Match` from the supplied headers, applies the patch via `IFhirPathPatchService`, re-indexes, writes a new `ResourceStore` row with `HttpVerbId.Patch`, bumps version from `previousResourceStore.VersionId + 1`. | `FhirPatchHandler.cs:79-194` |
| `FhirConditionalPatchHandler` = search → 0 matches ⇒ 404, >1 ⇒ 412, 1 ⇒ delegate to `IFhirPatchHandler`. Its match-count semantics are replicated in transaction pre-process (see design rationale below). | `FhirConditionalPatchHandler.cs:51-93` |
| Conditional-PUT pre-process reuses static helpers `FhirConditionalUpdateHandler.IsMoreThanOneResourceMatch / NoResourceMatch / IsSingleResourceMatch / ResourceIdProvided / MatchedResourceIdEqualsProvidedResourcedId`. | `FhirTransactionPutService.cs:199-298` |
| Endpoint policy record has all needed flags: `AllowUpdate`, `AllowConditionalUpdate`, `AllowPatch`, `AllowConditionalPatch`. | `src/Abm.Pyro.Application/EndpointPolicy/EndpointPolicy.cs` |
| DI registration of the service to rename. | `src/Abm.Pyro.Api/Program.cs:142` |
| `FhirTransactionService.UpdateResourceReferences` walks `entry.Resource.AllReferenceList()` / `AllUriList()` / `AllUuidList()` / `AllOidList()` generically for **every** entry — so `ResourceReference` values inside a PATCH `Parameters` body (e.g. a `urn:uuid:` pointing at a resource POSTed in the same bundle) get rewritten automatically. Narrative rewriting is guarded by `is DomainResource`, which `Parameters` is not — safe. | `FhirTransactionService.cs:258-303, 410-413` |
| Commit-phase entry failures set `FailureOperationOutcome`; orchestrator returns 400 with `CanCommitTransaction: false` ⇒ `DatabaseTransactionBehavior` rolls the whole DB transaction back. | `FhirTransactionService.cs:126-157, 246-256` |
| History-bundle rendering maps `HttpVerbId` → `Bundle.HTTPVerb` and **has no `Patch` case** (`default: throw ArgumentOutOfRangeException`). Already reachable today via direct PATCH + `_history`; must be fixed. | `src/Abm.Pyro.Domain/FhirSupport/FhirBundleCreationCreationSupport.cs:66-82` |
| Integration-test infra: `TransactionTests.cs` has private entry builders (`PostEntry`, `PutEntry`, `DeleteEntry`, `GetEntry`, `TransactionBundle`) — **no `PatchEntry` yet**. `Patch/PatchTests.cs` has a private `MakeOp(...)` builder for patch `Parameters`. | `src/Abm.Pyro.Api.Test/Transactions/TransactionTests.cs:353-412`, `src/Abm.Pyro.Api.Test/Patch/PatchTests.cs:27-50` |
| Batch bundles (`FhirBatchService`) are an unimplemented stub — out of scope. | `src/Abm.Pyro.Application/FhirBundleService/FhirBatchService.cs` |

### How resource-reference updating works for PATCH entries (verified against the code)

Transaction reference rewriting happens in `FhirTransactionService.UpdateResourceReferences`
(`FhirTransactionService.cs:258-303`), which runs **after** all pre-processing (so every
POST/PUT/PATCH entry's final resource id is already resolved into the metadata dictionary) and
**before** any commit. It walks `entry.Resource` of **every** bundle entry using the reflection
walkers in `src/Abm.Pyro.Application/Extensions/ResourceExtensions.cs` (`AllReferenceList()`,
`AllUriList()`, `AllUrlList()`, `AllUuidList()`, `AllOidList()`).

For a PATCH entry, `entry.Resource` is the **`Parameters` patch document**, and the walker
provably traverses it (verified in `ResourceExtensions.AllReferenceList`):
- `Parameters.parameter` is a collection of `Parameters.ParameterComponent`, whose direct base
  type is `BackboneElement` → matched by the BackboneElement branch and recursed into
  (`ResourceExtensions.cs:60-62, 132-151`); nested `part` components recurse the same way.
- `parameter.value[x]` is a datatype choice (`ChoiceType.DatatypeChoice`) → matched at
  `ResourceExtensions.cs:60`, and `AddIfTypeIsResourceReference` captures any `valueReference`.
- `parameter.resource` is of type `Resource` → also recursed.

Consequently a patch operation's `valueReference` (and `valueUri`/`valueUrl`/`valueUuid`/
`valueOid`) is rewritten by the existing machinery with **zero new code**:
`urn:uuid:`/`urn:oid:` fullUrl references and `ResourceType/id` references to same-bundle
POST/PUT entries are replaced with the final server ids (`UpdateBasedOnPutsAndPosts`), and
conditional references (`Patient?identifier=...`) inside patch values are resolved against the
server exactly like in any other entry (`ResolveConditionalReferences`).

**Scenario: the patch value references a resource CREATED (POST) in the same transaction.**
Supported end-to-end, and this is integration test 8 below. The sequence:
1. `PreProcessPosts` assigns the new server id for the POST entry and registers it in the
   metadata dictionary keyed by that entry's `fullUrl` (e.g. `urn:uuid:X`).
2. `UpdateResourceReferences` rewrites the `valueReference` `urn:uuid:X` inside the PATCH
   `Parameters` body to `Patient/{newServerId}`.
3. Commit order is DELETE → POST → PUT/PATCH (per the trules), so the POSTed resource row
   exists **before** `FhirPatchHandler` applies the patch and indexes the patched target — the
   resulting `IndexReference` rows point at a real resource.

**References already inside the stored target resource** need no handling: they were resolved
when the target was originally committed; a FHIRPath patch only mutates the elements named by
its operations, and the only door through which *new* reference values enter is the
`Parameters` body covered above.

Two deliberate boundaries (documented, not solved):
- **PATCHing a resource whose *identity* is created in the same transaction** (the patch
  *target*, not a patch *value*) is not supported: PATCH target resolution runs in pre-process
  against pre-transaction database state, so a direct PATCH of a not-yet-committed id, or a
  conditional PATCH whose criteria would only match a same-bundle POST/PUT-as-create, fails the
  transaction with the 404-style "PATCH does not create" outcome. This is intentional: a POST's
  server-assigned id is unknowable to the client when authoring the bundle (its `request.url`
  cannot name it), and a client that creates a resource in a bundle can simply compose the
  final state into the create entry itself. It is also consistent with the identity-overlap
  rule (two entries writing the same resource in one transaction SHALL fail).
- A patch value smuggled in as a plain `valueString` (e.g. targeting a `Reference.reference`
  primitive by path with a string value) is **not** rewritten — the walkers match typed
  `ResourceReference`/uri/url/uuid/oid values only. FHIRPath Patch semantics call for the
  value to carry the type of the element being set (`valueReference` for reference elements),
  so this is an acceptable, documented edge; mention it in the CLAUDE.md note.

### Design rationale: why the conditional-PATCH search runs in pre-process, not via `IFhirConditionalPatchHandler`

The transaction workflow **requires** the target resource id to be resolved during pre-process:
`ProcessPuts` asserts `ResourceUpdateInfo` is non-null for every claimed entry, and reference
rewriting/overlap detection need the resolved identity before commit. Calling
`FhirConditionalPatchHandler.Handle` at commit time would re-run the search too late and leave
`ResourceUpdateInfo` unset. So the conditional-PATCH match resolution mirrors
`PreProcessConditionalUpdate` (same search services, same static match-count helpers), with
PATCH semantics (0 matches ⇒ fail, never create), and the commit phase reuses
**`IFhirPatchHandler`** directly — exactly what `FhirConditionalPatchHandler` itself does after
its own search. This is maximal reuse consistent with the existing PUT pattern;
`FhirConditionalPatchHandler` remains untouched and continues to serve the HTTP endpoint.

---

## Implementation steps

### 1. Rename the service (files, types, members, call sites)

- `src/Abm.Pyro.Application/FhirBundleService/FhirTransactionPutService.cs`
  → `FhirTransactionPutAndPatchService.cs`, class `FhirTransactionPutAndPatchService`.
- `src/Abm.Pyro.Application/FhirBundleService/IFhirTransactionPutService.cs`
  → `IFhirTransactionPutAndPatchService.cs`, interface `IFhirTransactionPutAndPatchService`,
  methods renamed `PreProcessPuts` → `PreProcessPutsAndPatches`, `ProcessPuts` → `ProcessPutsAndPatches`.
- Update call sites:
  - `FhirTransactionService.cs` — constructor parameter (line 24) and the two invocations (lines 86, 146).
  - `Program.cs:142` — `builder.Services.AddScoped<IFhirTransactionPutAndPatchService, FhirTransactionPutAndPatchService>();`
- Use `git mv` (or plain rename) so history follows the files.
- Keep the repo's using conventions (`using Hl7.Fhir.Model;` + `using Task = System.Threading.Tasks.Task;`).

### 2. Pre-process: claim PATCH entries and resolve their targets

In `FhirTransactionPutAndPatchService`:

**2a. Entry filter.** Replace `GetPutEntry` with `GetPutOrPatchEntry` returning the entry when
`entry.Request?.Method is Bundle.HTTPVerb.PUT or Bundle.HTTPVerb.PATCH`. The pre-process loop
keeps its existing shared prologue for both verbs: parse `fullUrl` and `request.url`, `TryAdd`
the `BundleEntryTransactionMetaData` (duplicate `fullUrl` ⇒ existing failure), then branch on verb.

**2b. PUT branch — unchanged logic, fixed policy flags.**
- `IsConditionalPut(requestFhirUri)` true ⇒ check `AllowConditionalUpdate` (was `AllowConditionalCreate`), then existing `PreProcessConditionalUpdate`.
- Direct PUT ⇒ check `AllowUpdate` (was `AllowConditionalCreate`), then existing `PreProcessUpdate`.
- Keep the existing 403-style `FailureOperationOutcome` message shape; extract a small shared
  helper for the "endpoint policy refused" failure (verb name + fullUrl as parameters) since it
  will now be needed four times. While here, fix the two copy-paste errors in
  `ValidateUpdateRequest` message text ("as a POST action" → "as a PUT action"; "what found to
  be empty" → "was found to be empty").

**2c. PATCH branch — new.** A PATCH-specific validation + resolution path (do **not** funnel
PATCH through `ValidateUpdateRequest`: its resource-type-vs-URL check is wrong for PATCH,
whose `entry.resource` is a `Parameters`, not the target type).

`ValidatePatchRequest(requestFhirUri, patchEntry, metaData)` fails the entry when:
- `requestFhirUri.ResourceName` is empty (same wording as PUT's check, "as a PATCH action");
- `patchEntry.Resource is not Parameters` — message mirroring `PatchRequestValidator`'s
  "The body of a PATCH request must be a FHIR Parameters resource…";
- neither a `ResourceId` nor a `Query` is present on `request.url` (a bare `Patient` URL is
  neither a direct nor a conditional PATCH).

Then:
- **Conditional PATCH** (`ResourceId` empty, `Query` present — reuse `IsConditionalPut`, renamed
  `IsConditionalRequest`): check `AllowConditionalPatch`; run the same search pipeline as
  `PreProcessConditionalUpdate` (`fhirResourceTypeSupport.GetRequiredFhirResourceType(requestFhirUri.ResourceName)`
  → `searchQueryService.Process` → `validator.Validate(SearchQueryServiceOutcomeAndHeaders)` →
  `resourceStoreSearch.GetSearch`), then apply **PATCH match semantics** using the
  `FhirConditionalUpdateHandler` static helpers:
  - more than one match ⇒ `FailureOperationOutcome`: "…unable to be committed as a PATCH action.
    The conditional patch search query … returned a (412 Precondition Failed) response,
    indicating the criteria were not selective enough."
  - zero matches ⇒ `FailureOperationOutcome`: "…(404 Not Found)… PATCH does not create
    resources — use POST or PUT." (**never** falls through to create — this is the key
    divergence from conditional PUT).
  - exactly one match ⇒ `ResourceUpdateInfo(ResourceName: requestFhirUri.ResourceName,
    NewResourceId: matched.ResourceId, NewVersionId: matched.VersionId + 1,
    CommittedResourceInfo: null, ResourceStoreUpdateProjection: new ResourceStoreUpdateProjection(
    matched.ResourceStoreId, matched.VersionId, matched.IsCurrent, matched.IsDeleted))` —
    identical projection construction to `PreProcessConditionalUpdate`'s single-match arm.
- **Direct PATCH** (`ResourceId` present): check `AllowPatch`; load
  `await resourceStoreGetForUpdateByResourceId.Get(fhirResourceType, requestFhirUri.ResourceId)`;
  projection `null` **or `IsDeleted`** ⇒ `FailureOperationOutcome` "(404 Not Found)… PATCH does
  not create resources…"; otherwise `ResourceUpdateInfo` with
  `NewResourceId: requestFhirUri.ResourceId`, `NewVersionId: projection.VersionId + 1`, and the projection.

Note: `ResourceUpdateInfo.ResourceName` for PATCH must come from `requestFhirUri.ResourceName`
(never `entry.Resource.TypeName`, which is `"Parameters"`).

**2d. PUT/PATCH resolved-identity overlap detection.** Maintain a
`HashSet<string>` (ordinal) of `$"{ResourceName}/{NewResourceId}"` inside
`PreProcessPutsAndPatches`, adding each entry's resolved target when its `ResourceUpdateInfo`
is assigned. On a duplicate, set `FailureOperationOutcome`: "…the resource identity
{ResourceType}/{id} is the target of more than one PUT or PATCH entry within the Transaction
Bundle; per https://hl7.org/fhir/R4/http.html#trules overlapping resource identities SHALL
fail the transaction." (Conditional-PUT-as-create entries generate fresh GUIDs, so they add
safely without realistic collision.)

### 3. Commit: dispatch PUT vs PATCH per entry, in bundle order

Rework `ProcessPutsAndPatches` into a single pass over `entryList` (preserving entry order,
which is what the FHIR spec's step 3 "PUT or PATCH" grouping requires):

```
entry := GetPutOrPatchEntry(entryList[i]);  if null continue;
metaData := transactionResourceActionOutcomeDictionary[entry.FullUrl];
ArgumentNullException.ThrowIfNull(metaData.ResourceUpdateInfo);

if PUT:   existing flow — set entry.Resource.Id = NewResourceId, call fhirUpdateHandler.Handle(...)
if PATCH: patchParameters := (Parameters)entry.Resource;      // guaranteed by pre-process
          response := await fhirPatchHandler.Handle(
              tenant, requestId,
              resourceId:   metaData.ResourceUpdateInfo.NewResourceId,
              resourceName: metaData.ResourceUpdateInfo.ResourceName,
              patchParameters: patchParameters,
              headers: GetEntryRequestHeaders(entry, requestHeaders),   // renamed GetPutRequestHeaders
              cancellationToken: cancellationToken,
              previousResourceStore: metaData.ResourceUpdateInfo.ResourceStoreUpdateProjection);
```

Shared epilogue for both verbs (extract from the current PUT-only code):
- failure check: generalise `HasUpdateRequestFailed` → `HasRequestFailed(response, metaData, verbName)`
  so the message says "as a PUT action" / "as a PATCH action" correctly; on failure `break`.
- success: `entry.FullUrl = $"{metaData.ForFullUrl.PrimaryServiceRootServers}/{ResourceUpdateInfo.ResourceName}/{ResourceUpdateInfo.NewResourceId}"`,
  `entry.Resource = response.Resource` (the patched resource — or null under `Prefer: return=minimal`,
  matching existing PUT behaviour), and `entry.Response = new Bundle.ResponseComponent { Status, Etag, LastModified, Location }`
  built exactly as today (`fhirBundleCommonSupport.GetHeaderValue` / `fhirRequestHttpHeaderSupport.GetLastModified`).
  Use `ResourceUpdateInfo` (not `entry.Resource.TypeName/Id`) when composing `FullUrl` so the
  code is correct for both verbs.

New constructor dependency: `IFhirPatchHandler fhirPatchHandler` (already registered in DI,
`Program.cs:241`). Everything the PATCH path needs at commit time is inside the handler —
validation (`AllowPatch`), If-Match precondition (flows in via `entry.request.ifMatch` →
`GetRequestHeadersFromBundleEntryRequest`), FHIRPath patch application, profile validation
(`ValidateOnUpdate`), re-indexing, version bump, `ResourceStore` row with `HttpVerbId.Patch`,
and repository-event collection. An empty `Parameters` (no operations) is rejected by the
patch machinery with 400, which surfaces as an entry failure ⇒ whole transaction fails — the
desired behaviour; no extra pre-process check needed.

### 4. `FhirTransactionService` — rename call sites only

Constructor parameter and the two invocations become
`fhirTransactionPutAndPatchService.PreProcessPutsAndPatches(...)` / `.ProcessPutsAndPatches(...)`.
The step-3 comment ("Process any PUT or PATCH interactions") finally matches reality. No other
orchestrator changes: reference rewriting, failure aggregation, and rollback behaviour already
accommodate PATCH entries once they are registered in the metadata dictionary.

### 5. Fix the history-bundle verb mapping (latent bug, one line)

`src/Abm.Pyro.Domain/FhirSupport/FhirBundleCreationCreationSupport.cs` (~line 66): add
`case HttpVerbId.Patch: oResEntry.Request.Method = Bundle.HTTPVerb.PATCH; break;` to the
switch. Without it, a `_history` read of any PATCH-written version throws
`ArgumentOutOfRangeException` — already reachable today via the standalone PATCH endpoint.

### 6. Documentation

Update `CLAUDE.md`:
- In the **FHIRPath Patch** section: note that PATCH (direct and conditional) is supported
  inside transaction Bundles, handled by `FhirTransactionPutAndPatchService` reusing
  `IFhirPatchHandler`; conditional PATCH with 0 matches fails the transaction (PATCH never creates).
- In the **Integration Tests** PATCH-specific notes: add the transaction-PATCH test pattern
  (`Bundle.HTTPVerb.PATCH` entry with a `Parameters` resource body).

Optional cleanup (flag in the PR, do not block on it):
`src/Abm.Pyro.Application/FhirBundleService/BundleResourceReferenceUpdaterService.cs` is dead
code (referenced nowhere); it also branches on `HTTPVerb.PUT` and would otherwise mislead
future readers — recommend deleting it.

### 7. Integration tests (`src/Abm.Pyro.Api.Test`)

Add to `Transactions/TransactionTests.cs` (same class/collection so the existing private
helpers and Respawn fixture are reused):
- a `PatchEntry(string requestUrl, Parameters patchParameters, string? fullUrl = null, string? ifMatch = null)`
  helper (`Method = Bundle.HTTPVerb.PATCH`, `Resource = patchParameters`, fullUrl defaulting to
  a fresh `urn:uuid:`);
- a local `MakeOp(...)` `Parameters`-builder copied from the pattern in `Patch/PatchTests.cs:27-50`
  (or promote a shared builder into `Support/` if that is cleaner — implementer's choice).

Test cases (transaction failures assert `FhirOperationException` with `Status == 400` per the
existing test conventions; the whole-bundle rollback assertions verify a sibling POST/PUT in
the failing bundle was **not** committed by searching for it afterwards):

1. **Direct PATCH happy path** — create a Patient (seed via `FhirClient`), then transaction with
   one PATCH entry (`replace Patient.birthDate` or add of a name); assert entry `Response.Status`
   is `200 OK`, returned resource reflects the patch, `meta.versionId` bumped; follow-up read confirms persistence.
2. **Conditional PATCH, exactly one match** — seed a Patient with a unique identifier;
   PATCH `Patient?identifier=...`; assert patched.
3. **Conditional PATCH, zero matches** — transaction fails 400; OperationOutcome mentions PATCH
   does not create; sibling POST entry in the same bundle is rolled back.
4. **Conditional PATCH, multiple matches** — seed two matching Patients; transaction fails 400
   with the not-selective-enough message.
5. **Direct PATCH of a non-existent id** — transaction fails 400 (PATCH never creates).
6. **Direct PATCH of a deleted resource** — seed, delete, then PATCH in a transaction; fails 400.
7. **Mixed-verb bundle** — POST + PUT + PATCH + DELETE + GET in one transaction; all succeed;
   response entries line up positionally; PATCH applied.
8. **Patch value referencing a same-bundle POST** — bundle with POST Patient (`urn:uuid:X`) and
   PATCH on an existing Observation adding/replacing `Observation.subject` with reference
   `urn:uuid:X`; assert the committed Observation's subject is the real `Patient/{newId}`
   (exercises `UpdateResourceReferences` walking the `Parameters` body).
9. **PUT + PATCH overlap on the same target** — both aimed at the same seeded `Patient/{id}`;
   transaction fails 400 with the overlapping-identity message.
10. **If-Match precondition failure** — PATCH entry with `Request.IfMatch = "W/\"99\""` on a
    version-1 resource; transaction fails 400 and nothing is committed.
11. **Empty `Parameters` body** — PATCH entry whose `Parameters` has no operations; transaction fails 400.

## Verification

1. `dotnet build src/Abm.Pyro.CI.slnf` — clean build (rename touches Api + Application + Domain).
2. `dotnet test src/Abm.Pyro.CI.slnf` — full suite (Docker must be running for Testcontainers);
   all pre-existing transaction, PATCH, and policy tests must still pass — especially
   `Transactions/TransactionTests.cs` (regression on the rename + policy-flag fix) and `Patch/PatchTests.cs`.
3. End-to-end spot check (optional but recommended): `dotnet run --project src/Abm.Pyro.Api/Abm.Pyro.Api.csproj`,
   then POST a transaction bundle containing a PATCH entry to `/{tenant}/fhir` with
   `Content-Type: application/fhir+json`; confirm the transaction-response entry shows `200 OK`
   and a subsequent `GET /{tenant}/fhir/Patient/{id}/_history` renders (verifies step 5's verb-mapping fix).
4. No EF migration, cache, or configuration changes are required (`HttpVerbId.Patch` and the
   `AllowPatch`/`AllowConditionalPatch` policy flags already exist).

## Explicit non-goals

- Batch bundles (`FhirBatchService` is an unimplemented stub).
- Cross-verb identity-overlap detection involving DELETE/POST entries.
- Any change to the standalone PATCH endpoints, `FhirPatchHandler`, `FhirConditionalPatchHandler`,
  or `FhirPathPatchService` internals.
- PATCHing a resource whose identity is created by a POST/PUT **in the same bundle** — see the
  "How resource-reference updating works for PATCH entries" section for the full rationale.
  (Patch *values* referencing same-bundle creates ARE supported — that is test 8.)

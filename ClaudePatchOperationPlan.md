# FHIR Patch Operation — Implementation Plan

## Overview

This plan describes the full implementation of the FHIR R4 **FHIRPath Patch** operation into Pyro.
Only the FHIRPath Patch variant is in scope — the body is always a FHIR `Parameters` resource
(Content-Type `application/fhir+json` or `application/fhir+xml`). JSON Patch and XML Patch are
**out of scope**.

The design mirrors the existing `FhirUpdateHandler` / `FhirConditionalUpdateHandler` pair, with the
following key differences:

- The request body is a `Parameters` resource, not the resource-to-save.
- PATCH **never creates** a resource (no update-as-create path).
- A dedicated `FhirPathPatchService` applies the patch operations in memory before the result is
  persisted like a normal update.

---

## FHIR R4 Spec Summary — FHIRPath Patch

### HTTP routes

```
PATCH /{tenant}/{resourceName}/{resourceId}        → direct patch
PATCH /{tenant}/{resourceName}?{search-params}     → conditional patch
```

### Content-Type

`application/fhir+json` (or `+xml`). The standard FHIR MIME type — identical to any other FHIR
write. The server knows it is a patch because the HTTP method is `PATCH` and the body is a
`Parameters` resource.

### Request body — Parameters resource structure

```json
{
  "resourceType": "Parameters",
  "parameter": [
    {
      "name": "operation",
      "part": [
        { "name": "type",  "valueCode":    "<add|insert|delete|replace|move>" },
        { "name": "path",  "valueString":  "<fhirpath-expression>" },
        { "name": "name",  "valueString":  "<property-name>" },
        { "name": "value", "value[x]":     <typed-value> },
        { "name": "index", "valueInteger": 0 },
        { "name": "source",      "valueInteger": 0 },
        { "name": "destination", "valueInteger": 0 }
      ]
    }
  ]
}
```

Multiple operations = multiple `parameter` elements named `"operation"`. They are applied in order,
each to the result of the previous. The whole patch is atomic.

### Operation semantics

| Operation | `path` targets | Required parts | Notes |
|---|---|---|---|
| `add`     | parent element | `type`, `path`, `name`, `value` | Appends a new child property |
| `insert`  | the list       | `type`, `path`, `index`, `value` | Inserts into repeating list at 0-based index |
| `delete`  | the element    | `type`, `path` | Path must resolve to exactly one element |
| `replace` | the element    | `type`, `path`, `value` | Path must resolve to exactly one existing element |
| `move`    | the list       | `type`, `path`, `source`, `destination` | Reorders within a single list |

For every operation except `delete`: if `path` matches nothing, it is an error.

### HTTP response

| Condition | Status |
|---|---|
| Success | `200 OK` |
| Resource not found | `404 Not Found` |
| Conditional — 0 matches | `404 Not Found` |
| Conditional — multiple matches | `412 Precondition Failed` |
| `If-Match` version mismatch | `412 Precondition Failed` |
| Invalid patch / validation failure | `400 Bad Request` or `422 Unprocessable Entity` |

Response headers after 200: `ETag: W/"<versionId>"`, `Last-Modified: <utc>`.
Response body: honours the `Prefer: return=` header (same as Update).

### What PATCH must NOT do

PATCH never creates a resource. When the resource does not exist (direct or conditional with 0
matches), return 404 — there is no update-as-create path.

---

## Files to Create

| File | Purpose |
|---|---|
| `src/Abm.Pyro.Domain/FhirRequest/FhirPatchRequest.cs` | MediatR request record for direct PATCH |
| `src/Abm.Pyro.Domain/FhirRequest/FhirConditionalPatchRequest.cs` | MediatR request record for conditional PATCH |
| `src/Abm.Pyro.Application/FhirHandler/IFhirPatchHandler.cs` | Direct-call interface (for future batch/transaction use) |
| `src/Abm.Pyro.Application/FhirHandler/FhirPatchHandler.cs` | Handler — validates, loads, applies patch, persists |
| `src/Abm.Pyro.Application/FhirHandler/FhirConditionalPatchHandler.cs` | Handler — resolves ID by search, delegates to FhirPatchHandler |
| `src/Abm.Pyro.Application/Validation/PatchRequestValidator.cs` | Validator for `FhirPatchRequest` |
| `src/Abm.Pyro.Application/Validation/ConditionalPatchRequestValidator.cs` | Validator for `FhirConditionalPatchRequest` |
| `src/Abm.Pyro.Application/FhirPatch/IFhirPathPatchService.cs` | Interface for the patch applicator service |
| `src/Abm.Pyro.Application/FhirPatch/FhirPathPatchService.cs` | Applies FHIRPath patch operations to an in-memory resource |

---

## Files to Modify

| File | Change |
|---|---|
| `src/Abm.Pyro.Domain/Enums/HttpVerbId.cs` | Add `Patch = 5` with `[EnumInfo("PATCH", "Patch")]` |
| `src/Abm.Pyro.Domain/Configuration/ResourceEndpointPolicy.cs` | Add `AllowPatch` and `AllowConditionalPatch` bool properties |
| `src/Abm.Pyro.Application/EndpointPolicy/EndpointPolicy.cs` | Add `AllowPatch` and `AllowConditionalPatch` to the record |
| `src/Abm.Pyro.Application/EndpointPolicy/EndpointPolicyService.cs` | Wire up the two new fields in `LoadTenantDefaultEndpointPolicy`, `LoadTenantEndpointPolicyDictionary`, and `GetDenyAllEndpointPolicy` |
| `src/Abm.Pyro.Api/Controllers/FhirController.cs` | Add `Patch` and `ConditionalPatch` action methods |
| `src/Abm.Pyro.Api/Program.cs` | Register validators, handler (dual registration), and the patch service |
| `src/Abm.Pyro.Application/MetaDataService/MetaDataService.cs` | Add `Patch` entry to `GetRestResourceInteraction()` |
| `appsettings.json` | Add `AllowPatch: false, AllowConditionalPatch: false` to every policy block |

---

## Implementation Steps

### Step 0 — Spike: Validate the Firely ElementNode Mutation API

Before any handler code is written, write a small isolated test (console app or xUnit unit test) to
confirm the exact Firely SDK API for the mutation operations described in Step 7. Specifically:

1. Confirm that `ElementNode.FromElement(resource.ToTypedElement())` produces a tree where all
   nodes are `ElementNode` instances (so `Cast<ElementNode>()` is safe after `Select()`).
2. Confirm the `Add(IStructureDefinitionSummaryProvider, string name, object value, string? type)`
   signature for appending children.
3. Find or confirm the removal API — `ElementNode` may expose a `Remove(ElementNode child)` method,
   or children may need to be rebuilt by creating a new `ElementNode` via its constructor.
4. Confirm the round-trip: `ElementNode` → `node.ToJson()` → `new FhirJsonParser().Parse<Resource>()`.
5. Confirm that `PocoStructureDefinitionSummaryProvider` (from `Hl7.Fhir.R4`) is the correct
   `IStructureDefinitionSummaryProvider` implementation to pass to `Add`.

**Fallback:** If `ElementNode` does not expose a public removal or insertion-at-index API, fall back
to a JSON AST round-trip approach: serialize to `System.Text.Json.Nodes.JsonObject`, apply
operations directly on the JSON tree using FHIRPath navigation to locate nodes, then re-parse via
`FhirJsonParser`. This is less elegant but avoids Firely internals.

The spike result directly determines the implementation of `FhirPathPatchService`.

---

### Step 1 — `HttpVerbId` Enum

**File:** `src/Abm.Pyro.Domain/Enums/HttpVerbId.cs`

Add:
```csharp
[EnumInfo("PATCH", "Patch")]
Patch = 5
```

No EF Core migration is required. The enum value is stored as an `int` column (`ResourceStore.HttpVerb`)
and the new integer 5 is simply a new valid value. Existing rows are unaffected.

---

### Step 2 — Endpoint Policy

**File:** `src/Abm.Pyro.Domain/Configuration/ResourceEndpointPolicy.cs`

Add two properties after `AllowConditionalDelete`:
```csharp
public required bool AllowPatch            { get; init; } = false;
public required bool AllowConditionalPatch { get; init; } = false;
```

The `= false` default makes these backward-compatible with existing `appsettings.json` files that
do not yet specify them — the JSON binder will use `false` when the key is absent.

**File:** `src/Abm.Pyro.Application/EndpointPolicy/EndpointPolicy.cs`

Add `bool AllowPatch` and `bool AllowConditionalPatch` to the record's constructor parameters.

**File:** `src/Abm.Pyro.Application/EndpointPolicy/EndpointPolicyService.cs`

Three locations to update, mirroring the `AllowConditionalDelete` pattern exactly:
1. `LoadTenantDefaultEndpointPolicy` — read `tenantDefaultPolicy.AllowPatch` and
   `AllowConditionalPatch` into the `EndpointPolicy` constructor.
2. `LoadTenantEndpointPolicyDictionary` — declare local `bool allowPatch` and
   `bool allowConditionalPatch` initialised from `defaultEndpointPolicy`, run through
   `OnlySetIfFalse` for each enforceable policy, and pass to the `EndpointPolicy` constructor.
3. `GetDenyAllEndpointPolicy` — pass `AllowPatch: false, AllowConditionalPatch: false`.

**File:** `appsettings.json`

Add `"AllowPatch": false, "AllowConditionalPatch": false` to every policy object in
`ResourceEndpointPolicies.Policies`. Operators who wish to enable PATCH on a resource type will
set these to `true` in their configuration. The production server policy should be reviewed before
enabling.

---

### Step 3 — Request Records

**File:** `src/Abm.Pyro.Domain/FhirRequest/FhirPatchRequest.cs`

Model after `FhirUpdateRequest`. Inherits from `FhirResourceNameResourceRequestBase` so the
`[FromBody] Resource` model-binding path in the controller is identical to PUT. At runtime the
bound `Resource` will be a `Parameters` instance (because `Parameters : Resource` and the JSON
`resourceType` field controls deserialization). The handler casts it.

```csharp
public record FhirPatchRequest(
    string RequestSchema,
    string Tenant,
    string RequestId,
    string RequestPath,
    string? QueryString,
    Dictionary<string, StringValues> Headers,
    string ResourceName,
    Resource Resource,        // will be Parameters at runtime
    string ResourceId,
    DateTimeOffset TimeStamp)
  : FhirResourceNameResourceRequestBase(
        RequestSchema, Tenant, RequestId, RequestPath, QueryString,
        Headers, ResourceName, Resource, HttpVerbId.Patch, TimeStamp),
    IRequest<FhirOptionalResourceResponse>,
    IValidatable;
```

**File:** `src/Abm.Pyro.Domain/FhirRequest/FhirConditionalPatchRequest.cs`

Model after `FhirConditionalUpdateRequest`. No `ResourceId` property — the ID is discovered by the
search in the conditional handler.

```csharp
public record FhirConditionalPatchRequest(
    string RequestSchema,
    string Tenant,
    string RequestId,
    string RequestPath,
    string? QueryString,
    Dictionary<string, StringValues> Headers,
    string ResourceName,
    Resource Resource,        // will be Parameters at runtime
    DateTimeOffset TimeStamp)
  : FhirResourceNameResourceRequestBase(
        RequestSchema, Tenant, RequestId, RequestPath, QueryString,
        Headers, ResourceName, Resource, HttpVerbId.Patch, TimeStamp),
    IRequest<FhirOptionalResourceResponse>,
    IValidatable;
```

---

### Step 4 — Validators

**File:** `src/Abm.Pyro.Application/Validation/PatchRequestValidator.cs`

Extends `ValidatorBase<FhirPatchRequest>`. Checks:

1. `endpointPolicyService.GetEndpointPolicy(request.Tenant, request.ResourceName).AllowPatch`  
   → return 403 Forbidden if false.
2. `DoResourceNamesMatch` — URL `resourceName` must match the body resource type name. Since the
   body is a `Parameters` resource, this check should be **skipped** (or relaxed to not compare
   `Parameters` against the URL type). The `resourceName` in the URL is the target resource type,
   not the patch document type.
3. `IsRequestResourceIdPopulated` — `resourceId` in the URL must be non-empty.

> **Note on check 2:** The FHIR spec body for PATCH is always `Parameters`, not the target resource
> type. Do not validate `resourceType == resourceName` here; those will always mismatch.

**File:** `src/Abm.Pyro.Application/Validation/ConditionalPatchRequestValidator.cs`

Extends `ValidatorBase<FhirConditionalPatchRequest>`. Checks:

1. `AllowConditionalPatch` endpoint policy → 403 if false.
2. `QueryString` must be non-null and non-empty → 400 if missing (no search params = ambiguous).

No resource-name / resource-id checks (same reasoning as ConditionalUpdate — the ID is discovered
by the search).

---

### Step 5 — `FhirPathPatchService`

**File:** `src/Abm.Pyro.Application/FhirPatch/IFhirPathPatchService.cs`

```csharp
public interface IFhirPathPatchService
{
    Resource Apply(Resource target, Parameters patchParameters);
}
```

Throws `FhirException` (severity `Error`, HTTP 400) for any patch rule violation. The handler
catches this and converts it to an `OperationOutcome` response.

**File:** `src/Abm.Pyro.Application/FhirPatch/FhirPathPatchService.cs`

This is the highest-complexity piece. The algorithm is:

#### Setup

```
provider = new PocoStructureDefinitionSummaryProvider()
node     = ElementNode.FromElement(target.ToTypedElement())
```

`PocoStructureDefinitionSummaryProvider` comes from `Hl7.Fhir.R4` — it gives `Add()` enough
type information to assign the correct FHIR type to new child nodes.

#### Operation extraction

For each `parameter` in `patchParameters.Parameter` where `parameter.Name == "operation"`:

- `type`        = `parameter.Part.Single(p => p.Name == "type").Value` as `Code`
- `path`        = `parameter.Part.Single(p => p.Name == "path").Value` as `FhirString`
- `name`        = `parameter.Part.FirstOrDefault(p => p.Name == "name")?.Value` as `FhirString`
- `value`       = `parameter.Part.FirstOrDefault(p => p.Name == "value")?.Value` as `DataType`
- `index`       = `parameter.Part.FirstOrDefault(p => p.Name == "index")?.Value` as `Integer`
- `source`      = `parameter.Part.FirstOrDefault(p => p.Name == "source")?.Value` as `Integer`
- `destination` = `parameter.Part.FirstOrDefault(p => p.Name == "destination")?.Value` as `Integer`

Validate that required parts are present for each operation type before applying; throw `FhirException`
(400) if not.

#### FHIRPath navigation helper

```
IReadOnlyList<ElementNode> NavigatePath(ElementNode root, string fhirPath)
    → root.Select(fhirPath).Cast<ElementNode>().ToList()
```

This relies on the fact that `ElementNode.FromElement()` produces a pure `ElementNode` tree, so
`Cast<ElementNode>` is safe.

#### Per-operation logic

**`add`**
```
parent = NavigatePath(node, path).Single()
// Extract the primitive or POCO value from 'value' DataType
parent.Add(provider, name, extractedValue, fhirTypeName)
```

For complex `DataType` values (e.g., `HumanName`, `Identifier`): convert the Firely POCO to an
`ElementNode` subtree via `ElementNode.FromElement(value.ToTypedElement())`, then add each of its
children into `parent` under the given `name`. The exact merge strategy is to be confirmed in the
Step 0 spike.

**`insert`**
```
siblings = parent(path).Children(leafName).Cast<ElementNode>().ToList()
// Build new node for 'value'
// Insert into siblings list at 'index'
// Replace parent's children list
```

The exact mutation API depends on the spike result. If `ElementNode` supports direct child-list
manipulation, prefer that. Otherwise, reconstruct the parent `ElementNode` with the reordered
children list.

**`delete`**
```
target = NavigatePath(node, path)
// Validate exactly one match
parent = (ElementNode)target.Parent
parent.Remove(target)   // or rebuild parent children without target
```

**`replace`**
```
target = NavigatePath(node, path).Single()
parent = (ElementNode)target.Parent
// Replace target with new node built from 'value'
```

**`move`**
```
parent = NavigatePath(node, path).Single()
siblings = parent.Children(leafName).Cast<ElementNode>().ToList()
item = siblings[source]
siblings.RemoveAt(source)
siblings.Insert(destination, item)
// Reapply reordered children to parent
```

#### Round-trip back to Resource

```
json     = node.ToJson(new FhirJsonSerializationSettings { Pretty = false })
patched  = new FhirJsonParser().Parse<Resource>(json)
return patched
```

---

### Step 6 — `FhirPatchHandler`

**File:** `src/Abm.Pyro.Application/FhirHandler/FhirPatchHandler.cs`

Implements `IRequestHandler<FhirPatchRequest, FhirOptionalResourceResponse>` and `IFhirPatchHandler`.

#### Constructor-injected dependencies

Same as `FhirUpdateHandler` minus `IRequestHandler<FhirCreateRequest, ...>` (no create path), plus
`IFhirPathPatchService`.

#### Handler flow

```
1.  ValidatorResult = validator.Validate(request)  →  return 400/403 if invalid

2.  fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(request.ResourceName)
    // Use ResourceName from URL, not from Parameters body

3.  parameters = request.Resource as Parameters
    // Throw ApplicationException if cast fails (guard against misconfigured route)

4.  previousStore = await resourceStoreGetForUpdateByResourceId.Get(fhirResourceType, request.ResourceId)
    if (previousStore is null) → return 404 Not Found
    // PATCH never creates — this is the critical difference from Update

5.  IfMatchPreconditionFailure(request.Headers, previousStore.VersionId)  →  412 if mismatch

6.  currentResource = fhirDeSerializationSupport.ToResource(previousStore.Json)

7.  try:
        patchedResource = fhirPathPatchService.Apply(currentResource, parameters)
    catch FhirException:
        return 400 Bad Request with OperationOutcome

8.  // Ensure the patched resource retains the correct id and type
    patchedResource.Id = request.ResourceId

9.  // Optional FHIR profile validation (same guard as Update)
    if (serviceSettingsCache.GetFhirValidationSettings().ValidateOnCreate):
        outcome = await fhirValidateEngine.Validate(patchedResource)
        if (!outcome.Success) → return 400

10. indexerOutcome = await indexer.Process(patchedResource, fhirResourceType)

11. updatedVersionId = previousStore.VersionId + 1
    SetResourceMeta(patchedResource, updatedVersionId, request.TimeStamp)

12. Build updatedResourceStore (same shape as FhirUpdateHandler):
        resourceId      = patchedResource.Id
        versionId       = updatedVersionId
        isCurrent       = true
        isDeleted       = false
        resourceType    = fhirResourceType
        httpVerb        = HttpVerbId.Patch       ← differs from Update
        json            = fhirSerializationSupport.ToJson(patchedResource, ...)
        lastUpdatedUtc  = patchedResource.Meta.LastUpdated.Value.UtcDateTime
        index lists     = from indexerOutcome

13. previousStore.IsCurrent = false
    await resourceStoreUpdate.Update(previousStore, deleteFhirIndexes: ...)
    updatedResourceStore = await resourceStoreAdd.Add(updatedResourceStore)

14. repositoryEventCollector.Add(... RepositoryEventType.Update ...)

15. responseHeaders = fhirResponseHttpHeaderSupport.ForUpdate(...)

16. return preferredReturnTypeService.GetResponse(HttpStatusCode.OK, patchedResource, ...)
```

#### IFhirPatchHandler interface

**File:** `src/Abm.Pyro.Application/FhirHandler/IFhirPatchHandler.cs`

```csharp
public interface IFhirPatchHandler
{
    Task<FhirOptionalResourceResponse> Handle(
        string tenant,
        string requestId,
        string resourceId,
        string resourceName,
        Parameters patchParameters,
        Dictionary<string, StringValues> headers,
        CancellationToken cancellationToken,
        ResourceStoreUpdateProjection? previousResourceStore = null);
}
```

The `previousResourceStore` optional parameter mirrors `IFhirUpdateHandler` so the conditional
handler can pass in the already-fetched projection if available.

---

### Step 7 — `FhirConditionalPatchHandler`

**File:** `src/Abm.Pyro.Application/FhirHandler/FhirConditionalPatchHandler.cs`

Implements `IRequestHandler<FhirConditionalPatchRequest, FhirOptionalResourceResponse>`.

Mirrors `FhirConditionalUpdateHandler` exactly, with two critical differences:

1. There is **no create path** — `searchTotal == 0` always returns `404 Not Found`.
2. There is no resource-ID body to compare against the search result (the body is `Parameters`).

#### Handler flow

```
1.  validator.Validate(request)  →  403/400 if invalid

2.  fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(request.ResourceName)

3.  searchQueryServiceOutcome = await searchQueryService.Process(fhirResourceType, request.QueryString)
    validator.Validate(SearchQueryServiceOutcomeAndHeaders)  →  400 if invalid query

4.  resourceStoreSearchOutcome = await resourceStoreSearch.GetSearch(searchQueryServiceOutcome)

5.  switch (resourceStoreSearchOutcome.SearchTotal):
        > 1  → 412 Precondition Failed (multiple matches)
        == 0 → 404 Not Found             ← no create-on-patch
        == 1 → delegate to fhirPatchHandler.Handle(
                   tenant:         request.Tenant,
                   requestId:      request.RequestId,
                   resourceId:     resourceStoreSearchOutcome.ResourceStoreList.First().ResourceId,
                   resourceName:   request.ResourceName,
                   patchParameters: (Parameters)request.Resource,
                   headers:        request.Headers,
                   cancellationToken)
```

---

### Step 8 — Controller

**File:** `src/Abm.Pyro.Api/Controllers/FhirController.cs`

Add two action methods directly below the `ConditionalPut` method:

#### Direct PATCH

```csharp
[HttpPatch("{resourceName}/{resourceId}")]
public async Task<ActionResult<Resource>> Patch(
    string tenant,
    string resourceName,
    string resourceId,
    [FromBody] Resource resource,
    CancellationToken cancellationToken)
{
    var request = new FhirPatchRequest(
        RequestSchema: Request.Scheme,
        Tenant:        tenant,
        RequestId:     GuidSupport.NewFhirGuid(),
        RequestPath:   Request.Path,
        QueryString:   Request.QueryString.Value,
        Headers:       Request.Headers.GetDictionary(),
        ResourceName:  resourceName,
        Resource:      resource,
        ResourceId:    resourceId,
        TimeStamp:     dateTimeProvider.Now);

    FhirOptionalResourceResponse fhirResponse =
        await requestDispatcher.Send(request, cancellationToken);

    Response.Headers.AppendRange(fhirResponse.Headers);
    resource.AddAnnotation(SummaryType.False);
    return StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
}
```

#### Conditional PATCH

```csharp
[HttpPatch("{resourceName}")]
public async Task<ActionResult<Resource>> ConditionalPatch(
    string tenant,
    string resourceName,
    [FromBody] Resource resource,
    CancellationToken cancellationToken)
{
    var request = new FhirConditionalPatchRequest(
        RequestSchema: Request.Scheme,
        Tenant:        tenant,
        RequestId:     GuidSupport.NewFhirGuid(),
        RequestPath:   Request.Path,
        QueryString:   Request.QueryString.Value,
        Headers:       Request.Headers.GetDictionary(),
        ResourceName:  resourceName,
        Resource:      resource,
        TimeStamp:     dateTimeProvider.Now);

    FhirOptionalResourceResponse fhirResponse =
        await requestDispatcher.Send(request, cancellationToken);

    Response.Headers.AppendRange(fhirResponse.Headers);
    resource.AddAnnotation(SummaryType.False);
    return StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
}
```

The `[FromBody] Resource resource` binding already works for `Parameters` bodies because:
- Content-Type is `application/fhir+json` — the existing `JsonFhirInputFormatter` handles it.
- The formatter reads `resourceType: "Parameters"` and instantiates a `Parameters` object.
- `Parameters : Resource`, so the binding succeeds without any formatter changes.

---

### Step 9 — DI Registration

**File:** `src/Abm.Pyro.Api/Program.cs`

Add alongside the existing validator/handler blocks:

```csharp
// Validators
builder.Services.AddScoped<IValidatorBase<FhirPatchRequest>,            PatchRequestValidator>();
builder.Services.AddScoped<IValidatorBase<FhirConditionalPatchRequest>, ConditionalPatchRequestValidator>();

// Handlers — dual registration
builder.Services.AddScoped<IFhirPatchHandler,                                                FhirPatchHandler>();
builder.Services.AddScoped<IRequestHandler<FhirPatchRequest,            FhirOptionalResourceResponse>, FhirPatchHandler>();
builder.Services.AddScoped<IRequestHandler<FhirConditionalPatchRequest, FhirOptionalResourceResponse>, FhirConditionalPatchHandler>();

// Patch service
builder.Services.AddSingleton<IFhirPathPatchService, FhirPathPatchService>();
```

`IFhirPathPatchService` can be `Singleton` — it holds no mutable state; it only processes the
input parameters. The `PocoStructureDefinitionSummaryProvider` instance can be created once and
reused safely.

---

### Step 10 — CapabilityStatement

**File:** `src/Abm.Pyro.Application/MetaDataService/MetaDataService.cs`

In `GetRestResourceInteraction()`, add one entry:

```csharp
new CapabilityStatement.ResourceInteractionComponent()
{
    Code = CapabilityStatement.TypeRestfulInteraction.Patch
}
```

`TypeRestfulInteraction.Patch` exists in the Firely R4 SDK. This declares to FHIR clients that the
server supports the `patch` interaction on all resource types. It is a static declaration;
per-resource capability filtering (based on `AllowPatch`) is not implemented at the CapabilityStatement
level today for any other interaction, so this follows the existing pattern.

---

### Step 11 — Integration Tests

**File:** `src/Abm.Pyro.Api.Test/FhirPatch/FhirPatchIntegrationTests.cs`

All tests follow the existing `[Collection("IntegrationTestCollection")]` pattern using
`IntegrationTestFixture` and `FhirClient`.

Minimum test matrix:

| # | Scenario | Expected |
|---|---|---|
| 1 | PATCH existing resource — `replace` primitive field | 200; field changed; ETag incremented |
| 2 | PATCH existing resource — `add` new field | 200; field added |
| 3 | PATCH existing resource — `delete` an element | 200; element absent |
| 4 | PATCH existing resource — `insert` into list | 200; item at correct index |
| 5 | PATCH existing resource — `move` within list | 200; order changed |
| 6 | PATCH non-existent resource id | 404 |
| 7 | PATCH with `If-Match` matching current version | 200 |
| 8 | PATCH with `If-Match` version mismatch | 412 |
| 9 | PATCH with invalid FHIRPath in `path` part | 400 |
| 10 | Conditional PATCH — exactly 1 match | 200 |
| 11 | Conditional PATCH — 0 matches | 404 |
| 12 | Conditional PATCH — multiple matches | 412 |
| 13 | PATCH `replace` a non-existent element | 400 |
| 14 | Multi-operation patch (add then replace) | 200; both operations applied |
| 15 | PATCH when `AllowPatch: false` in policy | 403 |

---

## Technical Risks

### Risk 1 — `ElementNode` mutation API (HIGH)

`ElementNode` in `Hl7.Fhir.ElementModel` exposes `Add()` for appending children but the
public API for removing or inserting children at a specific index is not certain from documentation
alone. **Mitigate with Step 0 spike.** If the API is insufficient, the JSON round-trip fallback
(serialize to `JsonObject`, mutate, re-parse) is viable and the `IFhirPathPatchService` interface
isolates this decision — no other code changes.

### Risk 2 — Complex `value` types in patch parts (MEDIUM)

The spec allows the `value` part of an `add` or `insert` operation to be a complex backbone
element (e.g., `Patient.contact`) represented as nested `part` sub-elements rather than a named
FHIR type. This nested form is harder to map to `ElementNode` without additional parsing logic.
**Mitigate:** implement named-type support first (covers the vast majority of real-world patches);
document the nested-part limitation and add as a follow-up issue.

### Risk 3 — FHIRPath path ambiguity (LOW)

The FHIRPath engine evaluates paths against the `ITypedElement` tree. Some paths that are valid
FHIRPath expressions may resolve to type-specific variant nodes (e.g., `Observation.value[x]`
using `ofType()`). Confirm during the spike that the evaluated paths behave as expected for
common element addressing patterns.

---

## Out of Scope

- **JSON Patch** (`application/json-patch+json`) and **XML Patch** (`application/xml-patch+xml`).
- **Batch/Transaction PATCH entries**: The spec permits PATCH in a Transaction bundle. The
  `IFhirPatchHandler` interface is designed to support this in a future PR (mirrors `IFhirUpdateHandler`),
  but `FhirBatchOrTransactionHandler` is not modified here.
- **Nested backbone `part` elements** in patch operations (the anonymous complex-type path, see Risk 2).
- **Narrative regeneration**: Pyro does not manage FHIR narrative independently for any operation;
  the client is responsible for providing or omitting narrative in patch-produced resources. This
  matches the existing update behaviour.
- **CapabilityStatement per-resource patch flags**: Today no interaction entry is conditionally
  toggled per resource based on endpoint policy — this plan follows the same convention.

---

## Questions for You

These need answers before implementation begins.

**Q1 — Batch/Transaction wiring (scope gate)**
The `IFhirPatchHandler` interface is designed so that wiring PATCH into `FhirBatchOrTransactionHandler`
is a future-PR task. Is that scope boundary acceptable, or do you want PATCH entries in transactions
handled in this PR?

**Q2 — Step 0 spike: implementation preference**
Do you want me to write a unit test that proves the `ElementNode` mutation API works before writing
`FhirPathPatchService`, or should I proceed directly and fall back to the JSON round-trip if the
API turns out to be insufficient? The spike adds a day but catches the highest risk early.

**Q3 — `AllowPatch` in `appsettings.json` for production**
The new properties default to `false`. Should the existing production policy (which has
`AllowUpdate: true`) be updated to `AllowPatch: true, AllowConditionalPatch: true` in this PR, or
should the operator explicitly opt in? Getting this wrong in the production config could silently
block all PATCH requests.

**Q4 — Integration tests in scope?**
The test matrix above is 15 tests. Do you want them included in the same PR, or deferred to a
separate test PR after the feature lands?

**Q5 — PatchRequestValidator: body type guard**
The `PatchRequestValidator` receives a `FhirPatchRequest` where `request.Resource` should always
be a `Parameters` at runtime. Should the validator explicitly check `request.Resource is Parameters`
and return 400 if not (catching a client that sends a non-Parameters body), or is that guard better
placed in the handler? The validator seems like the right layer since it runs before business logic.

**Q6 — `SetResourceMeta` duplication**
Currently `SetResourceMeta` is a private static method duplicated across `FhirUpdateHandler`,
`FhirCreateHandler`, and `FhirDeleteHandler`. `FhirPatchHandler` will need the same logic.
Should we extract it to a shared static helper class (e.g., `ResourceMetaStamper`) in this PR,
or copy the pattern to keep the diff minimal and defer the cleanup?

**Q7 — 422 vs 400 for patch-produced validation failures**
The FHIR spec distinguishes `400 Bad Request` (invalid patch syntax, bad FHIRPath) from
`422 Unprocessable Entity` (patch applies cleanly but the result violates a profile). Do you want
to honour this distinction (map `IFhirValidateEngine` failures to 422 and patch-parse failures to
400), or always return 400 for simplicity?

# Location `near` Search Parameter — Design

**Date:** 2026-09-25
**Branch:** `feature/location-near-latitude-longitude-search`
**Status:** Approved design, ready for implementation planning

## Goal

Implement the FHIR R4 `near` search parameter on the `Location` resource:

```
GET [base]/Location?near=[latitude]|[longitude]|[distance]|[units]
```

`near` has search parameter type `special`, which Pyro currently refuses outright. This is the
only `special`-typed search parameter in the whole R4 search parameter seed, so implementing it
means turning on the `Special` code paths for exactly one canonical URL.

## Sources

- [Location resource, R4](https://hl7.org/fhir/R4/location.html)
- [Location §8.7.5.1 Positional Searching](https://hl7.org/fhir/R4/location.html#positional) — the
  "see notes" the search parameter description refers to. It carries requirements the parameter
  description alone does not.

## Current state of the codebase

`SearchParamType.Special` is stubbed out in three places:

| File | Today's behaviour |
|---|---|
| `src/Abm.Pyro.Application/Indexing/Indexer.cs:132` | Logs a warning, indexes nothing |
| `src/Abm.Pyro.Domain/SearchQuery/SearchQueryFactory.cs:107` | Mis-maps to `SearchQueryNumber` |
| `src/Abm.Pyro.Repository/Predicates/SearchSearchPredicateFactory.cs:50` | Throws `FhirFatalException` → HTTP 500 |

`FhirSearchQuerySupport` already declares `Special` as supporting the `:missing` modifier and no
prefixes, which matches the spec and needs no change.

`Location-near` is seeded at `src/Abm.Pyro.Repository/Seed/SearchParameterSeed.cs:18941` with
`type: "special"` and `expression: "Location.position"`.

### Facts established during design

- **`near-distance` does not exist in R4.** The parameter description's trailing sentence
  "Requires the near-distance parameter to be provided also" is leftover STU3 wording. §8.7.5.1
  states directly: *"the near-distance was deprecated as a result of this change too"*. The seed
  data contains no `Location-near-distance`. We implement `near` alone.
- **Chaining and `_has` come free.** `ChainedPredicateFactory.cs:85` and
  `HasPredicateFactory.cs:66` both route the *terminal* search parameter back through
  `searchPredicateFactory.GetResourceStoreIndexPredicate`. Once that method handles `Special`,
  `PractitionerRole?location.near=…` and `_has:Location:…:near=` work with no further change.
  They cost tests, not architecture.
- **No re-index facility exists.** Grep hits for "reindex" are false positives on
  `Sto`**`reIndex`**`Predicate`. See *Known limitations*.
- `ConfigurationSettingsExtension.cs` uses `ValidateDataAnnotations()`, not FluentValidation as
  `CLAUDE.md` states. Follow the code.

## Scope

**In scope**

- `near` filtering on `Location`, top-level.
- Comma-separated OR of multiple positions (R4 added this; STU3 lacked it).
- Returning each match's distance as the `location-distance` extension on
  `Bundle.entry.search`, behind a configuration flag.
- Chained and `_has` use of `near`.
- The `:missing` modifier.

**Explicit non-goals**

- `Location.position.altitude` is ignored. `near` has no altitude segment; distance is computed on
  the ellipsoid surface only.
- No `_sort` by distance. FHIR R4 defines no such sort.
- No general `special`-parameter framework. R4 has exactly one.
- No back-fill of already-stored Locations. See *Known limitations*.

## Approach

Storage uses a SQL Server `geography` column with a spatial index, via
`Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite` 10.0.12 (matching the solution's EF
10.0.12 exactly). `STDistance` returns **metres**, so metres is the canonical internal unit and all
unit handling collapses into parse-time conversion.

Returning the distance uses a **two-phase query**. The correlated-`EXISTS` predicate architecture
(`ResourceStore.IndexXList.Any(…)`) cannot surface a computed scalar, so:

- **Phase 1** — the existing pipeline, unchanged in shape:
  `EXISTS (… STDistance(Position, @point) <= @radius)`.
- **Phase 2** — takes the page's `ResourceStoreId`s and computes each one's distance.

Phase 2 was chosen over projecting the distance into the main query (which would force pagination,
the count query and `_include` to become near-aware) and over recomputing in C# (which would let
the filter and the reported distance disagree at the radius boundary, since two different
implementations of the geodesic would be involved). Here the same `STDistance` call does both jobs,
so they cannot disagree.

## 1. Data model

New entity `src/Abm.Pyro.Domain/Model/IndexPosition.cs : IndexBase`, following `IndexQuantity`:

| Member | Notes |
|---|---|
| `int? IndexPositionId` | PK, clustered — a SQL Server spatial index requires a clustered PK on the table |
| `Point Position` | `NetTopologySuite.Geometries.Point`, SRID 4326, `HasColumnType("geography")` |
| inherited | `ResourceStoreId`, `SearchParameterStoreId`, and their navigations |

`Location.position` is `0..1`, so at most one row per Location.

Storing `Latitude`/`Longitude` as separate doubles alongside `Position` was considered and
rejected: two sources of truth for one fact, and `geography` reads back fine.

**Dependency consequence.** The `Point` type puts a `NetTopologySuite` package reference into
`Abm.Pyro.Domain`, which is otherwise dependency-light. There is no way around this — SQL Server
spatial in EF Core requires NTS types on the entity for the LINQ predicate to translate. The EF
provider package goes in `Abm.Pyro.Repository` and `Abm.Pyro.Api`, and every `UseSqlServer` call
site gains `o => o.UseNetTopologySuite()`:

- `IPyroDbContextFactory`
- the design-time factory in `Abm.Pyro.Repository`
- `PyroWebApplicationFactory` in `Abm.Pyro.Api.Test`

**Supporting changes**

- `ResourceStore.IndexPositionList` navigation property.
- `PyroDbContext.IndexPosition` DbSet (its seventh).
- `IndexPositionEntityConfig` — FK to `ResourceStore` cascade, FK to `SearchParameterStore`
  no-action, per the convention in `IndexQuantityEntityConfig`.
- `IndexerOutcome.PositionIndexList`.
- `IndexPosition` added to `TablesToInclude` in `IntegrationTestFixture.cs` so Respawn clears it
  between integration tests.

**Migration** `AddIndexPositionTable`. EF models the table, foreign keys and ordinary indexes. The
spatial index cannot be modelled and needs raw SQL, with a matching `Down`:

```sql
CREATE SPATIAL INDEX SPATIAL_IndexPosition_Position
  ON IndexPosition(Position) USING GEOGRAPHY_AUTO_GRID;
```

No seed data lands in this table, so the Windows/Linux snapshot-determinism gotcha is low-risk
here — but `dotnet ef migrations has-pending-model-changes` must still be verified on Linux, since
the snapshot gains a geography mapping.

## 2. Indexing (write path)

New `IPositionSetter` / `PositionSetter` in `src/Abm.Pyro.Domain/IndexSetters/`, alongside the
existing seven setters.

`Indexer.cs:132` — the `SearchParamType.Special` arm checks the parameter's canonical URL. If it is
`http://hl7.org/fhir/SearchParameter/Location-near`, call the position setter; otherwise keep
today's warning log unchanged. A URL check, not a registry.

`PositionSetter.Set` receives the `Location.position` BackboneElement from the existing FHIRPath
evaluation, reads its `latitude` and `longitude` children, and:

- **Constructs `new Point(longitude, latitude) { SRID = 4326 }`.** X is longitude, Y is latitude.
  This is the highest-risk line in the feature: it fails silently rather than loudly, and the
  spec's own worked example has the two swapped (see *Traps*). It gets a dedicated unit test
  asserting `Point.X == longitude`.
- **Skips out-of-range coordinates with a warning rather than throwing.** `geography::Point`
  rejects latitude outside ±90 and longitude outside ±180, and nothing upstream prevents such a
  resource: FHIR types `Location.position.latitude` as a plain `decimal` with no range constraint,
  so neither the parser nor profile validation catches it. If the setter threw, one malformed
  Location would fail its own create/update with a 500. Instead the resource stores normally and is
  simply not findable via `near`.
- Emits no row when latitude or longitude is absent (defensive — both are `1..1`).

## 3. Configuration

New `src/Abm.Pyro.Domain/Configuration/LocationNearSettings.cs`, shaped like
`IncludeRevIncludeSettings` and registered in `ConfigurationSettingsExtension.cs`:

```json
"LocationNear": {
  "DefaultDistanceInMetres": 10000,
  "MaximumDistanceInMetres": 1000000,
  "ReturnDistanceInSearchResults": true
}
```

| Setting | Meaning |
|---|---|
| `DefaultDistanceInMetres` | Radius used when the client omits `[distance]`, which §8.7.5.1 leaves to server discretion. Ships at 10 km. |
| `MaximumDistanceInMetres` | Requests above this are rejected with a 400, so one query cannot scan the whole index. |
| `ReturnDistanceInSearchResults` | Turns phase 2 and the `location-distance` extension on or off. Defaults `true`. |

`[Range]` attributes bound at 20,000,000 m — half the earth's circumference, the largest meaningful
great-circle distance. The cross-field rule `DefaultDistanceInMetres <= MaximumDistanceInMetres`
needs `IValidatableObject` on the class, since `[Range]` cannot express it; `ValidateDataAnnotations`
honours that interface.

`ReturnDistanceInSearchResults` is server-wide rather than per-tenant, consistent with `Indexing`
and `Pagination`. **Filtering is unaffected by it** — `near` selects the same Locations either way;
the flag only controls whether the response says how far away they are.

## 4. Value parsing

New `SearchQueryNear : SearchQueryBase` and `SearchQueryNearValue : SearchQueryValueBase` in
`src/Abm.Pyro.Domain/SearchQueryEntity/`.

`SearchQueryFactory.cs:107` dispatches on canonical URL to `SearchQueryNear` and takes a new
`IOptions<LocationNearSettings>` dependency. A `Special` parameter that is *not* `Location-near` is
reported through the existing `UnsupportedSearchQueryList` path — never a 500, which is what
happens today.

`ParseValue` splits on the existing `OrDelimiter` (`,`), handles `:missing` via
`ParseModifierEqualToMissing` exactly as the other types do, then splits each term on `|` expecting
2–4 segments. Any other segment count is a 400.

| Segment | Rule |
|---|---|
| latitude | decimal, invariant culture, ∈ [−90, 90]; else 400 |
| longitude | decimal, invariant culture, ∈ [−180, 180]; else 400 |
| distance *(optional, may be empty)* | decimal > 0; omitted → `DefaultDistanceInMetres`; above `MaximumDistanceInMetres` → 400 |
| units *(optional, may be empty)* | table below; omitted → `km`, per spec |

Everything converts to **metres at parse time**. The term also retains the client's requested unit,
solely so the response extension can echo their units rather than imposing km.

### Accepted units

| Accepted | Metres |
|---|---|
| `km` | 1000 |
| `m` | 1 |
| `[mi_i]`, `mi`, `mile`, `miles` | 1609.344 |

Anything else returns a 400 `OperationOutcome` naming these three. Centimetres, feet, inches and
nautical miles are deliberately **not** supported.

No prefixes are accepted: `FhirSearchQuerySupport` already declares `Special` as prefix-less, and a
leading `gt`/`eq` fails the decimal parse regardless.

### What a comma-decimal actually does

Clients in comma-decimal locales will sometimes send `-33,87|151,21`. Such a value is rejected —
but only because it produces a malformed term, not because commas are detected as decimal
separators. The distinction matters and the guarantee is narrower than it first appears.

`,` is the OR separator in this grammar and FHIR decimals always use `.`, so a comma-decimal that
happens to yield well-formed terms is parsed as an OR of positions. `-10.5|20,5|7` denotes two
positions — `(-10.5, 20)` and `(5, 7)`, each at the default radius — and reading it that way is
correct.

This cannot be improved on. A legitimate multi-position search contains the same `digit,digit`
sequence: `33.8|151.2|5,37.8|144.9|5` is two positions with unsigned latitudes and omitted units.
Any lexical rule that rejected the typo would reject that valid search too. Detection is
impossible, not merely unimplemented, and both behaviours are pinned by tests so the outcome is
deliberate.

## 5. Filtering (query path)

`SearchSearchPredicateFactory.cs:50` — the `Special` arm stops throwing and becomes structurally
identical to its seven siblings:

```csharp
resourceStorePredicateFactory.PositionIndex(searchQuery)
  .ForEach(x => predicateInner = predicateInner.Or(y => y.IndexPositionList.Any(x.Compile())));
```

New `IIndexPositionPredicateFactory` / `IndexPositionPredicateFactory`, modelled on
`IndexQuantityPredicateFactory`. Per value it emits:

```csharp
i => i.SearchParameterStoreId == spId && i.Position.Distance(point) <= metres
```

which EF translates to `STDistance(…) <= @p` — the form SQL Server can serve from the spatial
index. `:missing` follows the existing factories' row-existence pattern.

Each comma-separated term becomes its own `Or`, so multi-position `near` is the standard OR
composition every other search type already uses.

Chained and `_has` forms need no changes here, per *Facts established during design*.

## 6. Returning the distance

§8.7.5.1:

> The distance between the location and the provided point is often used as one of the determining
> factors for selection of the location. So this value is included in the results. However the
> value cannot be inside the Location resource as it is different depending on the point of
> reference in the search. So the distance between is included in the search section of the bundle
> entry. Where multiple near positions are included, the distance to the closest point provided may
> be included.

`ResourceStoreSearchOutcome` gains one nullable member:

```csharp
IReadOnlyDictionary<int, NearDistance>? NearDistanceByResourceStoreId
```

null whenever the flag is off or the query had no `near` term, so all four other
`ResourceStoreSearchOutcome` producers and every existing call site are untouched.

```csharp
record NearDistance(double Metres, NearDistanceUnit ReportUnit);
```

Metres stays canonical; the unit is only what the client asked for.

**Phase 2**, in `ResourceStoreSearch.GetSearch`, runs only when `ReturnDistanceInSearchResults` is
true *and* a `SearchQueryNear` is present, and only after `targetResourceStoreList` is
materialised — so it sees one page (200 default, 1000 max), never the full result set.
`GetSearchTotalCount` is untouched.

It issues one keyed query per near term, each projecting `(ResourceStoreId, STDistance)` restricted
to that page's ids, then merges in C# taking the minimum and remembering which term won so the
right unit is echoed. Per-term queries rather than one combined query because `Math.Min` across N
points does not translate reliably to T-SQL, and N is almost always 1 — a pragmatic trade rather
than a clever expression tree.

Terms carrying the `:missing` modifier are skipped by phase 2, since they name no coordinate to
measure from. A `near:missing=true` search therefore matches Locations that have no position and
emits no `location-distance` extension for them, which is the only sensible outcome — there is no
distance to report. If a query combines `:missing=false` with no positional term, no extension is
emitted either.

**Bundle assembly.** `FhirBundleCreationCreationSupport.CreateBundle` passes the map into
`CreateEntry`, which already constructs `oResEntry.Search` at line 90. For
`SearchEntryMode.Match` entries only:

```
http://hl7.org/fhir/StructureDefinition/location-distance
  → Distance { Value, Unit, System = "http://unitsofmeasure.org", Code }
```

The spec's example carries only value and unit; adding `system` and `code` is valid and more
useful. A client asking in `mi` receives `unit = "mi"`, `code = "[mi_i]"`. Values round to 3 decimal
places in the reported unit. `_include` entries never receive the extension.

A chained `near` filters correctly but emits **no** extension, because the matched resources are
not Locations. This is correct — the extension describes the Location entry itself.

## 7. CapabilityStatement

`MetaDataService.cs:335` already maps `SearchParamType.Special` through to
`Hl7.Fhir.Model.SearchParamType.Special`, so `near` should begin advertising itself once indexing
works. This needs verifying rather than building — confirm it is not filtered out elsewhere.

## Behaviour changes beyond the feature

Both are improvements over today:

- `Location?near=…` returns results instead of HTTP 500.
- Any other `Special` parameter returns a 400 instead of HTTP 500.

## Traps

**Latitude/longitude ordering.** The normative parameter description specifies
`[latitude]|[longitude]`. The worked example in §8.7.5.1 is:

```
GET [base]/Location?near=-83.694810|42.256500|11.20|km
```

described as Ann Arbor — but Ann Arbor is latitude 42.28, longitude −83.74. **The example has the
two swapped.** It is a known erratum in the specification. We follow the normative order. Range
validation cannot catch this class of error, because −83.69 is itself a legal latitude; only the
explicit unit test on `Point.X` guards it.

**NTS `Point(x, y)` is `Point(longitude, latitude)`**, while T-SQL's `geography::Point(lat, long,
srid)` takes them the other way round. Anyone reading the raw SQL alongside the C# will meet both
orderings.

## Known limitations

**Existing Locations are invisible to `near` until re-written.** Indexing runs only on create and
update, and the server has no re-index facility. Every Location already in a deployed database will
have no `IndexPosition` row. A migration cannot fix this: building the index requires evaluating
FHIRPath over the stored JSON in C#, which SQL cannot do.

This is documented rather than solved. Operators re-save their Locations, or accept that only
Locations written after the upgrade are findable via `near`. A general re-index facility would
serve every index type, not just position, and is properly its own piece of work.

## Risks

**The one to prove before anything else is built:** that LinqKit's `.Any(predicate.Compile())`
expansion composes with an NTS `Point.Distance()` call and still yields SQL that *uses* the spatial
index rather than degenerating into a scan. LinqKit expression expansion and EF's spatial
translation are independent mechanisms and have not been exercised together in this codebase.

The implementation plan's first task is a throwaway probe against the Testcontainers SQL Server
that captures the actual execution plan. If the index is not used, the storage decision needs
revisiting before the rest of the feature is written on top of it.

## Testing

Following the existing three-suite split.

**`Abm.Pyro.Domain.Test`**

- `SearchQueryNear.ParseValue`: every syntax variant; all three units and their aliases; rejected
  units; out-of-range latitude and longitude; wrong segment counts; omitted distance → configured
  default; distance above the cap → 400; comma-separated OR; `:missing=true` and `:missing=false`.
- `PositionSetter`: valid position → one row; absent position → no rows; out-of-range coordinates →
  no row plus a warning; and the explicit `Point.X == longitude` ordering assertion.

**`Abm.Pyro.Api.Test`** — new `Location/NearSearchTests.cs`, with Locations seeded at coordinates
whose great-circle separations are known:

- inclusion and exclusion either side of the radius
- unit conversion (the same search expressed in `km`, `m` and `mi`)
- comma-separated OR across multiple positions
- the `location-distance` extension: present on matches, correct value, correct unit and code
- `ReturnDistanceInSearchResults = false` suppresses the extension while returning the same matches
- chained `location.near`, and `_has` with `near`
- `:missing`
- 400s for malformed values

**Fixture:** `IntegrationTestFixture.TablesToInclude` += `IndexPosition`.

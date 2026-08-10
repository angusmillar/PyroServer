# FHIR Search Index Optimisation Plan

> Status: **Draft for review** · Scope: **Full strategy + benchmarks** · Read/write posture: **Balanced** ·
> String matching: **spec starts-with only** · Target: **SQL Server / Azure SQL Managed Instance**

This plan covers the six FHIR search index tables (`IndexString`, `IndexToken`, `IndexReference`,
`IndexDateTime`, `IndexQuantity`, `IndexUri`) that back Pyro's FHIR R4 search, the queries run against
them, and a phased, measured programme to make them fast without unduly taxing ingest.

---

## 1. How search actually reaches these tables

All searches resolve to a query over `ResourceStore` with **correlated `EXISTS` subqueries** into the
index tables. LinqKit's `AsExpandable()` (see `ResourceStoreSearch.cs`) expands the `.Compile()` markers in
the predicate factories into real SQL — nothing is evaluated client-side.

A search such as `GET /Patient?name=smith&_count=20` becomes, in essence:

```sql
SELECT rs.*
FROM   ResourceStore rs
WHERE  rs.ResourceType = @patient AND rs.IsCurrent = 1 AND rs.IsDeleted = 0
  AND  EXISTS (SELECT 1 FROM IndexString ix
               WHERE ix.ResourceStoreId        = rs.ResourceStoreId   -- correlation (FK)
                 AND ix.SearchParameterStoreId = @nameSpId            -- which parameter
                 AND ix.Value LIKE @v + '%')                          -- the value test
ORDER BY rs.LastUpdatedUtc DESC
OFFSET (@page) ROWS FETCH NEXT (@count) ROWS ONLY;
```

The invariant across **every** index type is that each subquery filters on the triple:

```
(SearchParameterStoreId  [equality],  <value column(s)>  [equality | range | prefix])
```

and correlates back to the parent on `ResourceStoreId`. `_has` and chained searches add further nested
`EXISTS`/`IN` subqueries over `IndexReference`, filtered by
`(SearchParameterStoreId, ServiceBaseUrlId, ResourceType, ResourceId)` and joined on `ResourceId`.

**Design consequence:** the ideal index for each table leads with `SearchParameterStoreId`, continues with
the value column(s) in the order they are constrained, and *covers* the correlation column
`ResourceStoreId` via `INCLUDE`, so the `EXISTS` probe is a pure index seek with no key lookup.

---

## 2. Current state (from `PyroDbContextModelSnapshot`)

Every index table currently has **only single-column, non-covering nonclustered indexes**: the value
column(s) each indexed alone, plus the two FK columns (`ResourceStoreId`, `SearchParameterStoreId`) each
indexed alone by EF's relationship convention.

| Table | Value column(s) | Existing indexes (each single-column) |
|---|---|---|
| `IndexString` | `Value nvarchar(450)` | `Value`, `ResourceStoreId`, `SearchParameterStoreId` |
| `IndexToken` | `System nvarchar(450)`, `Code nvarchar(100)` | `System`, `Code`, `ResourceStoreId`, `SearchParameterStoreId` |
| `IndexReference` | `ResourceType int`, `ResourceId nvarchar(64) CS`, `VersionId`, `CanonicalVersionId`, `ServiceBaseUrlId` | `ResourceId`, `VersionId`, `CanonicalVersionId`, `ServiceBaseUrlId`, `ResourceStoreId`, `SearchParameterStoreId` |
| `IndexDateTime` | `LowUtc datetime2`, `HighUtc datetime2` | `LowUtc`, `HighUtc`, `ResourceStoreId`, `SearchParameterStoreId` |
| `IndexQuantity` | `Quantity decimal(18,6)`, `Code`, `System`, `Unit` (+ `*High`) | `Code`, `CodeHigh`, `Quantity`, `QuantityHigh`, `System`, `SystemHigh`, `ResourceStoreId`, `SearchParameterStoreId` |
| `IndexUri` | `Uri nvarchar(450)` | `Uri`, `ResourceStoreId`, `SearchParameterStoreId` |
| `ResourceStore` | — | unique `(ResourceId, ResourceType, VersionId)` only |

### Why this is slow

No single index matches the query's access pattern. For `name=smith` the optimiser can seek the `Value`
index — but that returns every `smith` across **all** search parameters and **all** resource types, then
must do key lookups to test `SearchParameterStoreId` and to fetch `ResourceStoreId` for the correlation.
For a common value (a popular LOINC `code`, `system='http://loinc.org'`, `gender=male`) the intermediate
set is large and the lookups are random I/O. Seeking the `SearchParameterStoreId` index instead is worse —
one parameter can own millions of rows. Either way the server pays for a mismatch between the index shape
and the query shape.

---

## 3. Proposed index design (per table)

Design rules (balanced posture — at most 1–2 net-new indexes per table, lean `INCLUDE` lists, and we
**drop redundant single-column indexes** to offset the write cost):

- Lead every index with `SearchParameterStoreId` (always an equality predicate).
- Order the remaining key columns by how they are constrained (equality columns before range columns).
- `INCLUDE (ResourceStoreId)` so the `EXISTS` probe is fully covered.
- Keep the FK index on `ResourceStoreId` (needed for cascade delete / re-index); **drop** the standalone
  `SearchParameterStoreId` and value-only indexes now subsumed by the composite's leading prefix.

### 3.1 `IndexString`
```
IX_IndexString_SpId_Value  =  (SearchParameterStoreId, Value)  INCLUDE (ResourceStoreId)
```
- Serves the (now spec-correct) `LIKE @v + '%'` default, `:exact` equality, and `:contains` (the latter
  scans, but only within one parameter's key range in a covering index — far cheaper than a table scan).
- `Value` moves to binary collation (§4.3), making the prefix/equality comparisons byte-wise.
- **Drop:** standalone `Value` and `SearchParameterStoreId` indexes.

### 3.2 `IndexToken`
```
IX_IndexToken_SpId_Code_System  =  (SearchParameterStoreId, Code, System)  INCLUDE (ResourceStoreId)
```
- `Code`-leading because **code-only** token search (`gender=male`, `status=final`) is by far the most
  common; this one index serves both code-only (key prefix) and code+system (full key). Values are already
  lower-cased at write time (`TokenSetter`), so binary collation applies (§4.3).
- System-only search (`|system` with no code) is rare; it residual-scans the parameter's key range.
  Add `IX_IndexToken_SpId_System (SearchParameterStoreId, System) INCLUDE (ResourceStoreId, Code)` **only**
  if Phase-0 profiling shows it is hot.
- **Drop:** standalone `Code`, `System`, `SearchParameterStoreId` indexes.

### 3.3 `IndexReference`
```
IX_IndexReference_SpId_Sbu_ResId_ResType
   = (SearchParameterStoreId, ServiceBaseUrlId, ResourceId, ResourceType)  INCLUDE (ResourceStoreId, VersionId)
```
- `ResourceId` is placed **before** `ResourceType` deliberately: direct reference search constrains both by
  equality (order between equality columns is immaterial to the seek), whereas `_has`/chained searches join
  on `ResourceId` **without** constraining the referenced `ResourceType` — so `ResourceId` must be reachable
  as a leading key. This one ordering serves direct search, the multi-id `IN` fast-path, `_has`, and chained.
- `ResourceId` keeps its case-sensitive collation (`SQL_Latin1_General_CP1_CS_AS`) to match
  `ResourceStore.ResourceId`.
- **Drop:** standalone `ResourceId`, `SearchParameterStoreId`, `ServiceBaseUrlId`. Keep `VersionId` /
  `CanonicalVersionId` indexes only if used elsewhere (verify in Phase 0).

### 3.4 `IndexDateTime`
```
IX_IndexDateTime_SpId_Low_High  =  (SearchParameterStoreId, LowUtc, HighUtc)  INCLUDE (ResourceStoreId)
```
- Covers `eq/ge/le` and most range predicates that lead with `LowUtc`.
- Some predicates lead with `HighUtc` (`gt`, parts of `lt`). Add
  `IX_IndexDateTime_SpId_High_Low (SearchParameterStoreId, HighUtc, LowUtc) INCLUDE (ResourceStoreId)`
  **only** if Phase-0 shows `gt/lt`-heavy date traffic; otherwise the first index residual-scans acceptably.
- **Drop:** standalone `LowUtc`, `HighUtc`, `SearchParameterStoreId`.

### 3.5 `IndexQuantity` (also serves `number` search)
```
IX_IndexQuantity_SpId_Code_Quantity
   = (SearchParameterStoreId, Code, Quantity)
     INCLUDE (System, Unit, Comparator, QuantityHigh, ComparatorHigh, ResourceStoreId)
```
- `Code`-then-`Quantity` matches the dominant `value-quantity=5.4|system|code` shape (equality on code,
  range on quantity). `System`/`Unit`/comparators are residual filters carried in `INCLUDE`.
- Number searches (no code) fall back to a `Quantity` range within the parameter range — acceptable; add a
  `(SearchParameterStoreId, Quantity)` index only if pure-number search proves hot.
- **Drop:** standalone `Code`, `System`, `Quantity`, and the `*High` value indexes (rarely selective
  alone), and `SearchParameterStoreId`.

### 3.6 `IndexUri`
```
IX_IndexUri_SpId_Uri  =  (SearchParameterStoreId, Uri)  INCLUDE (ResourceStoreId)
```
- Serves `:exact` (equality) and `:below` (prefix `LIKE @v + '%'`). `:above`/`:contains` residual-scan the
  parameter range. Binary collation applies (§4.3).
- **Drop:** standalone `Uri`, `SearchParameterStoreId`.

### 3.7 `ResourceStore` (the driving table)
```
IX_ResourceStore_Type_LastUpdated_current  (FILTERED)
   = (ResourceType, LastUpdatedUtc DESC)
     INCLUDE (ResourceStoreId, ResourceId)
     WHERE IsCurrent = 1 AND IsDeleted = 0
```
- Supports the ubiquitous `ResourceType = @t AND IsCurrent AND NOT IsDeleted` filter **and** the
  `ORDER BY LastUpdatedUtc DESC` paging, and covers the count/first-page driving set. The filter keeps it
  small (current, non-deleted rows only).
- Optional: add `IsCurrent` to the existing unique `(ResourceId, ResourceType, VersionId)` via `INCLUDE`
  to help `_has`/chained `ResourceId` joins, if Phase-0 shows those joins costing lookups.

### EF Core configuration example (`IndexStringEntityConfig`)
```csharp
builder.HasIndex(x => new { x.SearchParameterStoreId, x.Value })
       .IncludeProperties(x => x.ResourceStoreId)
       .HasDatabaseName("IX_IndexString_SpId_Value");

builder.Property(x => x.Value)
       .HasMaxLength(RepositoryModelConstraints.StringMaxLength)   // make the 450 cap explicit
       .UseCollation("Latin1_General_BIN2");                        // see §4.3
```
Filtered index on `ResourceStore` uses `.HasFilter("[IsCurrent] = CAST(1 AS bit) AND [IsDeleted] = CAST(0 AS bit)")`
— keep the literal identical across OSes so the model snapshot stays byte-identical (see §6).

---

## 4. Query / predicate correctness + sargability fixes

These change behaviour and are shipped as their own phase with test coverage.

### 4.1 `:missing` and `:not` are currently non-sargable **and** semantically wrong
Both are implemented as `SearchParameterStoreId != @spid` **inside** the `.Any()` subquery, producing
`EXISTS(… WHERE SearchParameterStoreId <> @spid)` — i.e. "this resource has *some other* indexed
parameter", which is neither what `:missing` nor `:not` means. (`IndexTokenPredicateFactory` already carries
a comment acknowledging the `:not` gap.)

**Fix — lift the negation to the `ResourceStore` level:**
- `param:missing=true`  →  `!rs.IndexXList.Any(i => i.SearchParameterStoreId == @spid)`  → SQL `NOT EXISTS(… = @spid)`
- `param:missing=false` →  `rs.IndexXList.Any(i => i.SearchParameterStoreId == @spid)`   → SQL `EXISTS(… = @spid)`
- `token:not=code`      →  `!rs.IndexTokenList.Any(i => i.SearchParameterStoreId == @spid && <value matches>)`
  — returns resources with a non-matching value **and** resources lacking the parameter entirely, per FHIR.

Both forms seek the new composite indexes (leading `SearchParameterStoreId`) cleanly.

### 4.2 Plain string search → spec starts-with only
`IndexStringPredicateFactory.StartsWithOrEndsWith` currently emits `Value LIKE @v% OR Value LIKE %@v`. The
`EndsWith` half (`LIKE '%smith'`) is never sargable and forces an index scan on the most common search type.
Per FHIR R4 string semantics (default = case/accent-insensitive **starts-with**), **remove the `EndsWith`
branch**, leaving `Value LIKE @v + '%'`, which seeks `IX_IndexString_SpId_Value`. Rename the helper to
`StartsWith`. Update any unit tests asserting the old behaviour.

### 4.3 Binary collation on normalised text columns
Stored values are already normalised at write time — `StringSetter.LowerTrimRemoveDiacriticsAndTruncate`
(lower-cased, trimmed, diacritics removed, truncated to 450) and `TokenSetter` lower-cases `Code`/`System`.
Because the data is pre-folded, switching `IndexString.Value`, `IndexToken.Code`/`System`, and
`IndexUri.Uri` to **`Latin1_General_BIN2`** makes equality and prefix comparisons byte-wise (faster seeks,
tighter index) with no semantic change.

**Prerequisite (correctness gate):** the search-term side must be folded **identically** before it reaches
the `LIKE`/`=` predicate, or BIN2 will miss matches. Phase 0 must verify `SearchQueryString`/token parsing
applies the same lower/trim/de-diacritic transform as the setters; add a shared normalisation helper if
there is any divergence. Do **not** ship §4.3 until this parity is proven by test.

`IndexReference.ResourceId` stays case-sensitive `..._CS_AS` (FHIR ids are case-sensitive) — no change.

---

## 5. Hashing and Full-Text — evaluated, not adopted as primary

### 5.1 Hashing — niche, deferred to optional Phase 4
A persisted computed hash column (`HASHBYTES('SHA2_256', …)`, optionally truncated to `binary(8)`) + index
accelerates **equality only**. It could tighten token `System+Code`, reference-key, and URI `:exact`
lookups and would sidestep the 450-char key limit. But it does **nothing** for the cases that dominate and
hurt: string prefix, date/quantity ranges, `:below/:above`. Our keys are already narrow (ints +
`nvarchar(100/450)`), so the composite covering indexes capture almost all of the benefit, and a hash needs
a residual predicate on the original value to resolve collisions. **Recommendation:** consider only for
`IndexToken`/`IndexReference` exact lookups **if** Phase 1–3 leaves a measured gap. Avoid `CHECKSUM`/
`BINARY_CHECKSUM` (weak, collision-prone, collation-dependent).

### 5.2 Full-Text Search — poor fit for the default, optional for `:contains`
FTS matches **word tokens**, not whole-field prefixes or arbitrary substrings: `CONTAINS(Value,'"smith*"')`
is prefix-of-*word*, which is not FHIR's prefix-of-*field* default, and FTS cannot express `:exact`. It only
genuinely helps `:contains` (`LIKE '%x%'`). It also adds a full-text catalog, asynchronous population lag,
and an operational dependency the default Testcontainers `mssql/server:2022` image does **not** satisfy
(the full-text feature is not installed in that image). **Recommendation:** solve default/`:exact`/prefix
with B-tree composites; revisit FTS as a *targeted* add-on only if `:contains` on free-text (e.g. narrative)
is shown to be a real hotspot, and budget the extra image/build work.

---

## 6. Operational, migration & portability constraints

- **Model-snapshot determinism.** Additive index migrations are safe, but the EF snapshot must stay
  byte-identical across Windows and the Linux CI/CD runner (see `CLAUDE.md`). Keep filter/collation strings
  as constant literals; verify with `dotnet ef migrations has-pending-model-changes` on both OSes.
- **Online vs offline builds (edition-dependent).** Target is SQL Server / Azure SQL MI. Azure SQL MI and
  Enterprise support `WITH (ONLINE = ON [, RESUMABLE = ON])`; SQL Server Standard supports online index
  operations from 2019 onward (older Standard = offline only). EF migrations emit plain `CREATE INDEX`
  (offline, portable). For large existing tables, hand-edit the generated migration or run the index builds
  **out of band** with `ONLINE = ON, RESUMABLE = ON` on MI/Enterprise for a zero-downtime deploy.
- **Data compression.** These rows are narrow and highly repetitive (`SearchParameterStoreId`, systems,
  codes) — excellent candidates for `DATA_COMPRESSION = PAGE`, typically 40–70% size reduction and fewer
  logical reads. Available on MI/Enterprise and Standard 2016 SP1+. Apply per-index in the out-of-band
  script; keep it out of the portable EF migration if any target lacks it.
- **Write amplification (balanced posture).** Net index count per table is held flat or lower: we add one
  composite and drop the redundant single-column indexes it subsumes. Ingest inserts the same number of
  index rows; each maintains ~the same number of indexes, now better-shaped. Confirm the tally in Phase 1.
- **Historic-row interaction.** `RemoveHistoricResourceIndexesOnUpdateOrDelete` affects index-row volume and
  therefore index size and the chained-search plans; capture metrics with this flag in its production state.

---

## 7. Benchmark harness (Phase 0 — build first, keep for regression)

**Goal:** reproducible before/after numbers per FHIR search type, on a realistic dataset, on the real
engine.

- **Engine & data.** Testcontainers `mssql/server:2022` (matches integration tests). Seed a representative
  corpus (parameterise size, e.g. 100k / 1M resources across Patient, Observation, Encounter,
  DiagnosticReport) with realistic value cardinality (skewed codes/systems, common surnames).
- **Query set** (one per access pattern): string prefix (`name=`), string `:exact`, string `:contains`,
  token code-only (`gender=`), token system+code, `:missing`/`:not`, reference (direct + multi-id `IN`),
  chained (`subject.name=`), `_has`, date range (`date=ge…&date=le…`), quantity with code, `_count` paging
  with `LastUpdatedUtc` sort.
- **Metrics.** Capture `SET STATISTICS IO, TIME ON` (logical reads + CPU/elapsed) and the actual execution
  plan per query; assert seeks over scans on the target indexes. Prefer logical reads as the primary,
  hardware-independent signal; record wall-clock as secondary. Optionally enable Query Store for aggregates.
- **Form.** Extend the existing `Abm.Pyro.Domain.Benchmark` project (or a new xUnit "perf" collection using
  the integration `IntegrationTestFixture` plumbing) so runs are one command and comparable across phases.
- **Baseline.** Record current-schema numbers for the whole query set before any change — this is the
  yardstick every later phase is measured against.

---

## 8. Phased rollout (each phase re-runs the Phase-0 harness)

| Phase | Content | Risk | Behaviour change |
|---|---|---|---|
| **0** | Benchmark harness + seeded dataset + baseline capture; verify §4.3 normalisation parity; confirm which optional indexes (token system-only, datetime high-leading, pure-number) are actually hot | none | none |
| **1** | Additive composite/covering indexes (§3) + drop subsumed single-column indexes, via EF migration | low | none |
| **2** | Query/predicate fixes: `:missing`/`:not` → `NOT EXISTS` (§4.1); remove string `EndsWith` (§4.2); tests | medium | yes (correctness) |
| **3** | Binary-collation migration on normalised columns (§4.3) — drop/rebuild affected indexes; gated on Phase-0 parity proof | medium | none (semantics preserved) |
| **4** | *Optional, profile-driven only:* hash columns for token/reference exact (§5.1); FTS for a proven `:contains` hotspot (§5.2) | higher | additive |

Ship Phase 1 on its own first — it is pure additive schema with the largest, lowest-risk win. Only proceed
to later phases where the harness shows a remaining, quantified gap.

---

## 9. Success criteria

- Target index **seeks** (not scans) on all Phase-0 query types except intrinsically non-sargable ones
  (`:contains`, `:above`), verified in execution plans.
- Material reduction in logical reads for the common patterns (string prefix, token code, reference,
  chained, `_has`) — set concrete targets from the Phase-0 baseline (e.g. ≥5× fewer logical reads on
  `name=` and `code=`).
- No regression in ingest throughput beyond an agreed balanced-posture budget (measure insert/update of a
  resource batch before/after Phase 1).
- `:missing`/`:not` return spec-correct result sets (new tests), and string search matches FHIR R4
  starts-with semantics.

---

## 10. Open items for you

1. **SQL Server edition & version** (e.g. Azure SQL MI, or on-prem 2019/2022 Standard vs Enterprise). This
   decides online/resumable index builds and PAGE compression in Phase 1 — the only remaining input needed
   before Phase 0 starts.
2. **Dataset scale to target** for the benchmark corpus (expected production row counts per top resource
   type) so the harness reflects reality.
3. Confirm the historic-index flag's **production** setting so benchmarks match live index volumes.

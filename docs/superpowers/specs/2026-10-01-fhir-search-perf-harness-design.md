# FHIR Search Performance — Programme Design and Sub-Project A (Measurement Harness)

> Date: 2026-10-01 · Status: **Approved design, ready for implementation planning** ·
> Supersedes `2026-09-27-fhir-search-perf-harness-design.md` ·
> Obsoletes `docs/superpowers/plans/2026-09-29-fhir-search-perf-harness.md`

This document has two parts. **Part I** records the programme: the review of the prior plan, the
decomposition into sub-projects, and the sequencing decisions. **Part II** is the design for
sub-project A, the measurement harness, which is the only part being built now.

## Note on supersession

The 2026-09-27 design chose a deterministic in-repo corpus generator and explicitly rejected
Synthea (its §5.4). **That decision is reversed.** The corpus is now Synthea FHIR R4 transaction
bundles, read from a configurable directory outside the repository.

Part I is carried forward substantially intact — its review of the prior index plan (§2) and its
sequencing argument (§4) are independent of the corpus decision and remain accurate. Part I §1's
corpus row and the whole of §5 are rewritten. **Part II is rewritten throughout**, because the
generator was not one component among nine but the premise the other components were arranged
around.

The reversal is recorded rather than tidied away, in §5.4. A reader who wants to know why this
project owns no generator should be able to find out that it once planned to, and what changed.

---

# Part I — Programme

## 1. Purpose and brief

Make Pyro's FHIR search fast against `ResourceStore` and the seven search-parameter index tables
(`IndexString`, `IndexToken`, `IndexReference`, `IndexDateTime`, `IndexQuantity`, `IndexUri`,
`IndexPosition`), without unduly taxing ingest.

Decisions taken by the project owner during design:

| Decision | Choice |
|---|---|
| Scope | Index design **and** search correctness **and** search-pipeline query shape |
| Working data scale | 527,113 resources — the whole Synthea sample set |
| Read/write posture | Aggressive — drop subsumed indexes *and* question the column design itself |
| Re-index capability | In scope, as a first-class server operation |
| Corpus | **Synthea FHIR R4 transaction bundles, from a configurable local directory outside the repository** |
| Corpus lifecycle | Load once through the real pipeline, snapshot, reuse; reset from the snapshot; reload on demand |
| CI posture | The harness is a developer-local tool. No CI performance gate. |
| Programme structure | Strictly sequential, correctness first, then harness |

Constraints derived from the repository rather than stated by the owner:

- **Azure SQL, with the app holding only `db_datareader`/`db_datawriter`.** The application cannot
  alter its own schema, so every schema change ships as an EF migration through the CD `migrate`
  job, which runs as `pyro_migration_user`. There is no path for the running server to create an
  index.
- **EF model snapshots must stay byte-identical across Windows and Linux** (`CLAUDE.md`), because
  snapshots are authored on Windows and verified on the Linux CI/CD runner.
- **`IndexPosition` does not follow the other six tables' rules.** It is served by a spatial index,
  whose access pattern is structurally different (see §2.2e).

## 2. Review of the prior plan

`assets/plans/fhir-index-plan.md` was reviewed against the current code. Its central reasoning is
**correct and is carried forward**.

### 2.1 Confirmed

- **The core diagnosis.** Every index subquery filters on `(SearchParameterStoreId` equality,
  value column(s)`)` and correlates back to the parent on `ResourceStoreId`, while every existing
  index on every index table is **single-column**. Verified against
  `Abm.Pyro.Repository/Migrations/PyroDbContextModelSnapshot.cs`.
- **The current-state inventory** in the prior plan's §2 is accurate for all six tables it covers,
  and `ResourceStore` really does carry nothing but the unique
  `(ResourceId, ResourceType, VersionId)` index — no index supports the ubiquitous
  `ResourceType = @t AND IsCurrent AND NOT IsDeleted` filter or the
  `ORDER BY LastUpdatedUtc DESC` paging.
- **The non-sargable string default.** `IndexStringPredicateFactory.cs:93` emits
  `Value LIKE @v + '%' OR Value LIKE '%' + @v`. The `EndsWith` half can never seek.
- **The write-time normalisation** the prior plan wants to exploit for binary collation does exist
  (`StringSetter.LowerTrimRemoveDiacriticsAndTruncate`, and `TokenSetter` lower-casing
  `Code`/`System`).
- **Its rejection of hashing and full-text search as *primary* strategies.** The reasoning — that
  hashing helps equality only while the dominant painful cases are prefix and range, and that FTS
  matches word tokens rather than field prefixes and is absent from the
  `mcr.microsoft.com/mssql/server:2022-latest` image — is sound and is adopted unchanged.

### 2.2 Corrected

**a. `:missing` is not merely "semantically wrong" — it is broken in two different ways, and the
`true`/`false` value is discarded entirely.**

`IsMissing` is parsed onto every search value type (`SearchQueryValueBase.IsMissing`), but **only
`IndexPositionPredicateFactory` ever reads it**. For every other search type, `:missing=true` and
`:missing=false` produce identical SQL.

| Factories | Predicate produced | Effect |
|---|---|---|
| `IndexString` (:52), `IndexUri` (:51), `IndexReference` (:84), `IndexQuantity` (:93), `IndexDateTime` (:101) | `spid == @x AND spid != @x` | **Self-contradictory — zero rows, always** |
| `IndexToken` (:38), `IndexNumber` (:91) | `true AND spid != @x` | "has some *other* indexed parameter" — wrong rows |
| `IndexPosition` | `NOT EXISTS(… spid == @x)` | **Correct** |

The five self-contradictory cases arise because those factories apply
`And(IsSearchParameterId(...))` unconditionally before the modifier switch; the two "other
parameter" cases apply it only inside the no-modifier branch.

`IndexNumberPredicateFactory.cs:92` carries a commented-out
`AnyIndexEquals(IndexQuantityPredicate, !NumberValue.IsMissing)` — the correct shape, written and
abandoned. This raises `:missing` from a sargability concern to a correctness defect, and raises
its priority accordingly.

**b. The fix pattern already exists in the codebase.** `IndexPositionPredicateFactory.PositionIndexMissing`
performs exactly the `ResourceStore`-level negation the prior plan proposes, with a doc comment
explaining why an index-row predicate cannot express it. `SearchSearchPredicateFactory` routes the
`Special` + `Missing` case to it. Sub-project B1 is therefore "generalise the `near` pattern to the
other search types", which is cheaper and lower-risk than the prior plan's framing.

**c. The prior plan misses the double execution of the search predicate.**
`ResourceStoreSearch.GetSearch` runs `CountAsync()` (`:56`) and then the paged query (`:67`) —
the full, expensive predicate twice per search. The `COUNT` cannot be short-circuited by paging,
so it touches every matching row while the page touches `_count` rows. For large result sets this
is plausibly the single largest available win. FHIR's `_total` parameter provides the standard
licence to make it optional.

**d. The prior plan asserts an index direction it should be measuring.** Its proposed
`(SearchParameterStoreId, Value) INCLUDE (ResourceStoreId)` only pays off if the optimiser drives
*index-table-first*. For a non-selective value against a selective resource type, driving
*`ResourceStore`-first* is cheaper and wants the correlation column early —
`(SearchParameterStoreId, ResourceStoreId, Value)`. Both shapes are defensible. Which wins is a
measurement question, and answering it is a primary purpose of sub-project A (§11, §19.8).

**e. `IndexPosition` needs a different design rule, not the same one.** The prior plan predates the
table and is silent on it. The rule "lead with `SearchParameterStoreId`, `INCLUDE (ResourceStoreId)`"
does not transfer:

- `STDistance(…) <= @radius` can only be served by a spatial index. `SearchParameterStoreId` cannot
  be placed ahead of a spatial key, and spatial indexes cannot carry `INCLUDE` columns.
- `SearchParameterStoreId` is effectively **constant** across the whole table, because only
  `Location.position` writes to it. It has no selectivity, so `IX_IndexPosition_SearchParameterStoreId`
  is pure write cost today and a composite leading with it would be pointless.
- What the table plausibly needs instead is correlation-side coverage for the `ResourceStore`-first
  direction, plus the existing `ResourceStoreId` FK index that `NearDistanceQuery`'s page-scoped
  second pass already depends on.

**f. Columns that are written on every ingest and never read.** `IndexQuantity.CodeHigh`,
`IndexQuantity.SystemHigh`, `IndexQuantity.UnitHigh` and `IndexReference.CanonicalVersionId` appear
in no predicate or query. Three of them additionally carry their own nonclustered index.
`IndexQuantity` carries **eight** nonclustered indexes in total, `IndexReference` **six**.

**g. Two smaller items.** The prior plan's Open Item #1 (SQL Server edition) is already answered by
`CLAUDE.md` — Azure SQL, with all DDL flowing through the CD `migrate` job. And its §7 suggestion to
extend `Abm.Pyro.Domain.Benchmark` will not work: that is a BenchmarkDotNet in-process project
(string support, FHIR URI factory) with no database involvement at all.

## 3. Decomposition

The approved scope is five sub-projects, not one. Each gets its own spec, plan and implementation
cycle.

| | Sub-project | Depends on | Independent? |
|---|---|---|---|
| **B1** | Search correctness — `:missing` and `:not` → `ResourceStore`-level negation | — | fully |
| **A** | **Measurement harness** — loader, profiler, query set, plans, baselines | B1 | see §4.1 |
| **B2** | Remove the non-sargable string `EndsWith` branch | — | fully |
| **C** | Re-index operation — re-run setters over stored JSON, scoped by type/parameter | — | fully (benefits from A) |
| **D** | Index and column redesign across the eight tables | A, B1, B2, C | no |
| **E** | Query shape — the double `CountAsync`, `_total` support | A | fully |

Search correctness is split into B1 and B2 because its two halves differ in kind: `:missing`/`:not`
is pure correctness with no measurement value, whereas removing the string `EndsWith` branch is a
correctness fix with a direct *performance* consequence, and is the change that makes the proposed
`IndexString` composite worth building.

**Note for sub-project C.** The 2026-09-27 design justified a `BulkIndexWriter` component in A
partly as the seam C would reuse — "run the real setters over a resource and write index rows in
bulk" *is* re-indexing. That component no longer exists (§5.4, §9). **C inherits no bulk-write
seam** and should be designed knowing that. C is not harmed by the change: A's loader drives
527,113 resources through the real create pipeline, which is stronger evidence that the real setter
path scales than a purpose-built bulk writer would have been.

## 4. Sequencing, and why the prior plan's order is wrong

The prior plan ships indexes in its Phase 1 and fixes queries in its Phase 2. That order is
**inverted**, because the Phase 2 fixes change the very query shapes the Phase 1 indexes were
designed to serve:

- `:missing` today is a contradictory or mis-scoped `EXISTS`. After the fix it is
  `NOT EXISTS(… spid = @x)` — a different access pattern with different index needs. An index
  designed against today's shape is designed against a query that returns nothing.
- The default string search today is `LIKE @v% OR LIKE '%'+@v`, which **cannot seek no matter what
  index exists**. The proposed `(SearchParameterStoreId, Value)` index only pays off *after* the
  `EndsWith` branch is removed.

Sequencing indexes first would measure new indexes against queries about to be replaced,
understate their benefit, and risk choosing column orders validated against obsolete shapes.

**Approved order: B1 → A → B2 → (C ∥ E) → D.** Correctness precedes index design; index design comes
last, with full evidence.

### 4.1 Why B1 precedes the harness

An earlier draft put the harness first on "baseline before you change" grounds. That discipline
governs *performance* work; it does not govern a bug fix that changes result sets, because there is
nothing meaningful to baseline about a query that returns zero rows. B1 therefore runs **before** A:

1. **It is a live defect with no error signal.** `:missing` returns zero rows or wrong rows today,
   silently. Fixing that does not require a harness to be worth doing.
2. **Baselining it first would poison the baseline.** §14's query set covers `:missing` on five
   search types. Measured today, each records "0 rows, ~0 logical reads, instant", and after the fix
   the same entries read as a catastrophic regression. §12.1's cardinality field and §16's zero-row
   flag prevent *misreading* that, but not baselining a broken query is cleaner than defending
   against the misreading.
3. **There is no dependency in that direction.** Correct `:missing` semantics are provable with a
   handful of resources in `Abm.Pyro.Api.Test`; they need nothing the 527k corpus provides.
4. **B1 is structural, and A builds on the structure.** `IResourceStorePredicateFactory` returns
   `List<Expression<Func<IndexX, bool>>>` for all six index types, with `PositionIndexMissing`
   bolted on beside `PositionIndex` as the single `ResourceStore`-level escape hatch. Fixing
   `:missing` properly means either six more `XxxIndexMissing` methods or reshaping that interface —
   and A's `QueryRunner` drives exactly that layer. The shape should settle before a query set and
   committed baselines are built on top of it.

B2 stays after A, so the sargability gain from removing `EndsWith` gets a real before-and-after.
With snapshot/reset (§10) the "before" figure is recoverable at any time by restoring the snapshot
and re-measuring an earlier revision, so this is a preference, not a constraint.

**Known gap, to be scoped during B1's design:** `ChainedPredicateFactory` and `HasPredicateFactory`
contain no `:missing` handling at all, so the chained and `_has` forms (for example
`Patient?general-practitioner.name:missing=true`) are presently unspecified rather than merely
wrong. Whether B1 covers them or they become a separate item is a B1 scope decision.

## 5. The corpus

### 5.1 What the corpus is

The Synthea FHIR R4 sample set `synthea_sample_data_fhir_r4_sep2019`, measured as follows:

| Property | Value |
|---|---|
| Files | 1,180 patient transaction bundles, `*.json` |
| On-disk JSON | 1.3 GB uncompressed |
| **Total resources** | **527,113** |
| Entries per bundle | median 273, mean 447, **max 18,488** |
| Largest bundle | 42 MB; 3 bundles exceed 30 MB, 8 exceed 10 MB, 29 exceed 5 MB |
| Bundle shape | `type: transaction`; every entry `POST`; every `fullUrl` a `urn:uuid`; no `ifNoneExist`; some `#contained` references |
| Resource types | 19 |

Resource type census:

| Type | Count | Share |
|---|---|---|
| Observation | 259,929 | 49.3% |
| Claim | 60,970 | 11.6% |
| Encounter | 46,868 | 8.9% |
| ExplanationOfBenefit | 46,868 | 8.9% |
| Procedure | 36,451 | 6.9% |
| DiagnosticReport | 18,304 | 3.5% |
| Immunization | 15,013 | 2.8% |
| MedicationRequest | 14,102 | 2.7% |
| Condition | 8,766 | 1.7% |
| CareTeam | 3,603 | 0.7% |
| CarePlan | 3,603 | 0.7% |
| Practitioner | 2,980 | 0.6% |
| Organization | 2,979 | 0.6% |
| Goal | 2,969 | 0.6% |
| Patient | 1,180 | 0.2% |
| ImagingStudy | 977 | 0.2% |
| MedicationAdministration | 926 | 0.2% |
| AllergyIntolerance | 567 | 0.1% |
| Device | 58 | 0.0% |

**One scale, not a ladder.** The 2026-09-27 design proposed 10k / 250k / 750k. The 10k rung existed
to serve a CI gate that no longer exists (§1), and the 750k rung was hypothetical. The whole sample
set loads in one pass and `--max-files` (§8) remains available for ad-hoc smaller runs, so there is
no reason to maintain a ladder. Measuring at the largest scale available, always, removes a class of
"which scale was that figure from" confusion from every later sub-project.

Storage is **not** estimated in this document. The 2026-09-27 design carried an estimate table
derived from assumed compression ratios and index-row counts; replacing it with measured fact is
sub-project A's first deliverable (§13), and no index decision is taken before those figures exist.
The order-of-magnitude figures in §5.3's location table exist only to say where each artifact lives
and whether it can reach git — they are labelled as estimates and are not inputs to any decision.

### 5.2 Buffer pressure is a dial, not a data volume

Rather than growing data to outrun available RAM, the harness **caps the SQL container's memory**.
The corpus is whatever Synthea provides; buffer pressure is configured independently, defaulting to
a cap that makes the corpus not quite fit. This is unchanged from the 2026-09-27 design and is now
the *only* lever on I/O behaviour, since corpus size is no longer adjustable by generation.

### 5.3 Storage: the data lives outside the repository, the fingerprint lives inside it

The 2026-09-27 design's principle was "store the recipe, not the cake" — the corpus was a pure
function of a seed, so it was reproducible rather than archived. **That is inverted.** Synthea data
is not reproducible by this project at all. What git holds is a *pointer and a fingerprint*.

| Artifact | Size | Location | In git? |
|---|---|---|---|
| Harness source | ~50–100 KB | `src/Abm.Pyro.Performance/` | **Yes** |
| Corpus fingerprint (loader version, ordered file list + sizes, `--max-files`, resulting census) | ~60 KB | `assets/perf/corpus-manifest.json` | **Yes** |
| Corpus profile (selectivity statistics, §11) | ~50 KB | `assets/perf/corpus-profile.json` | **Yes** |
| Query set definitions | ~10–20 KB | `assets/perf/queries/` | **Yes** |
| Baseline results | ~100 KB per run | `assets/perf/baselines/` | **Yes** — text, diffs well |
| **Synthea bundles** | **1.3 GB** | configured directory, outside the repo | **No** |
| Seeded database (mdf/ldf) | GBs | named Docker volume | No |
| `.bak` snapshot | ~1 GB est. | named Docker volume | No |

Total git impact for the whole programme: **well under 2 MB**, all of it reviewable text. The
baselines remain the most valuable artifact in the repository — every later claim rests on them.

Three safeguards, because scrubbing a multi-gigabyte accidental commit from history is painful:

1. **The configured corpus path has no default pointing inside the repository tree.** A missing
   configuration value is an error, not a fallback. `.gitignore` is the second line of defence, not
   the only one.
2. **The database and the `.bak` live in named Docker volumes**, not host bind mounts, so neither is
   on a filesystem path `git add` can reach. This also avoids the `mssql` image's non-root uid
   colliding with Windows bind-mount permissions.
3. **Git LFS is not used.** LFS is for large files that genuinely need versioning; externally
   sourced input data and derived database files are both the wrong case for it.

### 5.4 Synthea: dropped, then adopted

The 2026-09-27 design considered Synthea and rejected it, on two grounds: that it is the one corpus
that cannot be cheaply regenerated, and that a generator controls cardinality and skew *better*
than Synthea does because a distribution can be dialled to probe a specific index question.

Both grounds were correct. The decision was reversed anyway, because what they weigh against was
undervalued:

- **A generator can only produce the skew its author thought to specify.** §2.2d's index-direction
  question is about how the optimiser behaves across a selectivity range, and a generator answers it
  against a distribution chosen by the same person forming the hypothesis. Synthea's skew was not
  chosen by anyone here.
- **The corpus is far richer than the generator was going to be.** The 2026-09-27 design generated
  five resource types. Synthea supplies nineteen, including `Claim` and `ExplanationOfBenefit` —
  large, reference-dense resources with deeply nested structures that a generator would not have
  produced and that exercise the indexing path harder than anything it would have.
- **Reference fan-out is real.** ~220 Observations per Patient, against the 50:1 the generator was
  specified for.
- **Non-reproducibility is answered by the snapshot, not by the generator.** The 2026-09-27 design
  treated reproducibility as a property of generation. It is better served as a property of
  *storage*: a `.bak` taken after load is restorable in well under a minute, portable, and
  self-identifying (§10). That mechanism was already half-present in the old design as an optional
  feature; promoting it to core makes the regeneration argument moot.

What is genuinely lost is dialability, and the loss is real: two index tables now receive no
measured evidence at all (§14.1). That cost is accepted and recorded rather than minimised.

---

# Part II — Sub-Project A: Measurement Harness

## 6. Scope

A one-command, reproducible measurement of every FHIR search access pattern against a 527,113-resource
Synthea corpus loaded through Pyro's own write path, capturing logical reads and actual execution
plans, with committed baselines that every later sub-project is measured against.

Sub-project A changes **no** production behaviour. It touches no index, no predicate, no handler.

## 7. Project shape

A new console project `src/Abm.Pyro.Performance` (.NET 10), added to `src/Abm.Pyro.CI.slnf` so CI
compiles it (per `CLAUDE.md`, new .NET 10 projects must be added to the filter). CI compiles it and
runs its database-free unit tests (§17). CI does not run a performance gate — see §1.

```
load      --corpus <path> [--max-files N] [--force]
snapshot
reset
profile
run       --queries all --baseline pre-phase-b
compare   --from pre-phase-b --to post-phase-b
ingest    --count 5000
```

Components, each with a single responsibility and independently testable:

| Component | Responsibility | Touches DB? |
|---|---|---|
| `CorpusHost` | container lifecycle, migrations, memory cap, corpus-identity check | yes |
| `CorpusLoader` | directory → real transaction pipeline, in-process (§9) | yes |
| `SnapshotManager` | `BACKUP`/`RESTORE`, connection draining (§10) | yes |
| `CorpusProfiler` | loaded index tables → selectivity statistics (§11) | yes |
| `QueryRunner` | FHIR query string → real search pipeline → captured SQL | yes |
| `PlanAnalyser` | captured SQL → logical reads + plan operators | yes |
| `IndexShapeProbe` | hand-written SQL against candidate index shapes (§12) | yes |
| `BaselineStore` | baseline JSON read/write | no |
| `ReportWriter` | markdown comparison report | no |

Component count is unchanged from the 2026-09-27 design, but `CorpusGenerator`,
`DistributionProfile` and `BulkIndexWriter` are gone and the code volume drops substantially. The
more important change is to the risk profile: **nothing in the harness reimplements indexing
semantics any more.** The old design's largest correctness risk was `BulkIndexWriter` writing index
rows that diverged subtly from what the real create path writes. That risk is now structurally
impossible rather than merely tested for.

## 8. The corpus contract

The harness owns no data. It consumes a directory, and states its assumptions as checks rather than
hopes.

- **Configured path.** From the performance project's `appsettings.json`, overridable by environment
  variable and command line. **No default pointing inside the repository tree**; absent
  configuration is an error (§5.3).
- **Expected shape.** `*.json`, each a FHIR R4 `Bundle` of `type: transaction`. Validated on read;
  failures name the offending file (§16).
- **Load order is sorted filename order.** The only ordering the harness imposes, and it makes a
  partial load well-defined.
- **`--max-files N`** limits the load to the first N files in that order. **Default: every `*.json`
  in the directory** — not the literal 1,180, so pointing the harness at a different export still
  works. 1,180 and 527,113 are measured facts about *this* export (§5.1), recorded in the manifest,
  never constants in code.
- **Resource count is an output, not an input.** A transaction bundle is the atomic load unit, and
  bundles range from 26 to 18,488 entries, so a `--max-resources` knob could never deliver the number
  it named. The loader reports the resulting resource count and per-type census after load and writes
  both to the manifest, so a partial corpus is still self-describing.

Two properties of partial loads, recorded so a future reader does not rediscover them:

- **Small subsets are lumpy.** The corpus is heavy-tailed; one 42 MB bundle holds 18,488 resources,
  3.5% of everything, in a single file. `--max-files 10` may be dominated by one patient depending on
  where the giants fall alphabetically. Irrelevant at the default; relevant to anyone reaching for a
  small N.
- **Sorted order makes a subset an alphabetical prefix.** Synthea draws given names from gender- and
  ethnicity-linked pools, so an alphabetical prefix mildly skews those attributes. Harmless for the
  full corpus. If a subset's representativeness ever matters, the fix is a stride (every k-th file)
  rather than a prefix. Not built now.

**Corpus identity** = a hash over (loader version, ordered `(filename, byte size)` for every file
loaded, `--max-files` if set). Cheap to compute, and it detects the realistic accidents: a different
Synthea export, a half-copied directory, a changed subset. §16's refuse-to-run check is built on it.

## 9. The loader

`CorpusLoader` replaces `CorpusGenerator`, `DistributionProfile` and `BulkIndexWriter`.

For each file in sorted order: deserialise with the Firely SDK, build a `FhirTransactionRequest`,
and **invoke the handler in-process through DI** — the same way §12's `QueryRunner` resolves a real
`IResourceStoreSearch`. The transaction pipeline performs `urn:uuid` → `ResourceType/id` rewriting
(`FhirTransactionService.UpdateResourceReferences`) and the real index setters run, because this
*is* the real create path. No HTTP, so Kestrel's default 30 MB request-body limit never applies —
which matters, because three bundles exceed it and the largest is 42 MB with 18,488 entries. That
bundle remains one database transaction and will be slow, but it will complete.

Two preconditions asserted **before** the load starts, rather than failing four hundred bundles in:

1. **Endpoint policy.** The loader censuses the directory first, then checks that the performance
   tenant's `ResourceEndpointPolicies` entry permits transaction POST for all 19 resource types the
   census found.
2. **FHIR profile validation is off for the performance tenant.** With it on, a 527k-resource load
   is impractical and Synthea data is likely to be rejected on profile grounds. This is written into
   every write-cost figure the harness reports as a stated caveat, not treated as a silent
   convenience: **all ingest measurements in this programme exclude profile-validation cost.**

`load --force` is required to overwrite a database that already holds a corpus. Without it, loading
over an existing corpus is refused.

## 10. Lifecycle: load, snapshot, reuse, reset, reload

### 10.1 Why the snapshot is load-bearing, not an optimisation

`FhirCreateHandler.cs:80` assigns ids via `GuidSupport.NewFhirGuid()`, and `LastUpdatedUtc` is
wall-clock. **Two loads of the same directory therefore produce genuinely different databases.** They
agree on row counts, on the per-type census, and on every value `CorpusProfiler` cares about, but
they disagree on id values, on physical row placement within the index B-trees, and on the order
`ORDER BY LastUpdatedUtc DESC` returns — so deep paging returns a different page of resources
entirely.

The snapshot is what makes a baseline comparable to a measurement taken a week later. Reload is the
deliberately destructive operation, not the route back to a clean corpus.

### 10.2 Mechanism

`BACKUP DATABASE` / `RESTORE DATABASE` as plain T-SQL against the running container. No container
lifecycle involvement; the container stays up.

```sql
-- snapshot, once, immediately after load completes
BACKUP DATABASE [PyroPerf] TO DISK = '/var/opt/mssql/backup/pyroperf.bak'
  WITH INIT, COMPRESSION, CHECKSUM;

-- reset, any time after
ALTER DATABASE [PyroPerf] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [PyroPerf] FROM DISK = '/var/opt/mssql/backup/pyroperf.bak' WITH REPLACE;
ALTER DATABASE [PyroPerf] SET MULTI_USER;
```

- The `.bak` lives in a **second named Docker volume**, `pyro-perf-backup`, mounted at
  `/var/opt/mssql/backup` — consistent with §5.3's safeguards and with the data files' volume.
- `mcr.microsoft.com/mssql/server:2022-latest` defaults to Developer edition, so `COMPRESSION` is
  available.
- **The connection drain is the fiddly part.** `SET SINGLE_USER WITH ROLLBACK IMMEDIATE` fails while
  the harness holds connections, and ADO.NET pools them past `DbContext` disposal. `reset` therefore
  runs as a distinct step **before** the application host is built, over its own `SqlConnection` to
  `master`, preceded by `SqlConnection.ClearAllPools()`. Resetting from inside a running host with
  Pyro's DI graph alive does not work.

### 10.3 Why not SQL Server database snapshots

`CREATE DATABASE … AS SNAPSHOT OF` with `RESTORE … FROM DATABASE_SNAPSHOT` looks strictly better —
copy-on-write sparse files, near-instant revert, supported on Developer edition. It is rejected, and
specifically because §10.6's `ingest` survives:

**While a database snapshot is live, every write to the source database triggers a copy-on-write page
push into the sparse file.** That is a direct tax on precisely the thing `ingest` exists to measure,
and the distortion varies with how much has been written since the snapshot, so it is not even a
constant offset that could be reasoned around. A harness that measures write cost must not run on a
database configured to make writes more expensive.

Database snapshots also die with the container and cannot be copied out, where a `.bak` survives a
`DROP DATABASE` and survives the container.

### 10.4 The manifest travels with the backup

The harness creates a `PerfCorpusManifest` table in the performance database holding the corpus
identity hash, the ordered file list with sizes, `--max-files`, the loader version, the resulting
resource count and the per-type census. EF ignores tables it has no entity for, so this disturbs
neither migrations nor the snapshot-determinism rule in `CLAUDE.md`.

Because the manifest is *inside* the database, it travels with the `.bak`. A restored database
self-identifies, so §16's refuse-to-run check works after a reset, after a reload, and after the
`.bak` is moved to another machine. Without it, a restored database is anonymous and the identity
check could only compare against whatever the harness last wrote locally.

Side benefit, noted and not built for: the `.bak` is a portable, self-describing pre-loaded corpus
containing only synthetic data. Anyone holding it can skip the load entirely and never needs the
1.3 GB Synthea directory.

### 10.5 The lifecycle, and the rule the harness enforces

| Command | Effect | Duration |
|---|---|---|
| `load` | fills an empty database through the real transaction pipeline; writes the manifest | the slow one, once — unmeasured, see §13 |
| `snapshot` | `BACKUP … WITH COMPRESSION` | est. 1–2 min |
| `profile` | reads index tables, writes `corpus-profile.json` | minutes |
| `run` | read-only measurement | per query set |
| `ingest --count N` | **mutates** — appends resources to the corpus | seconds |
| `reset` | `RESTORE … WITH REPLACE` | est. 30–90 s |
| `load --force` | destructive reload; the write-cost instrument for sub-project D | the slow one again |

**`run` refuses to start if the database has been mutated since the snapshot**, detected by comparing
the live `ResourceStore` row count against the count the manifest recorded. Run an `ingest`, and the
next `run` instructs you to `reset` first. This is enforced, not documented.

### 10.6 Two complementary write measurements

Sub-project D intends to drop indexes and recompose columns, justified partly as "this makes writes
cheaper". That claim needs evidence, and one measurement is not enough, because filling an empty
table and writing into a full one are different things:

| Measurement | Shape | What it answers |
|---|---|---|
| **Load throughput**, reported by `load` as a by-product | fill-from-empty, transaction-bundle path, 527k resources | bulk/migration-shaped write cost. Load with index set X, ship the migration, `load --force`, compare. The delta *is* the write cost of the index change. |
| **`ingest --count N`** | steady-state single-resource create against the fully loaded corpus | operational write cost with realistic index depth. Deep B-trees, real page splits, one commit per resource touching every index on every table — the pattern most sensitive to index *count*. Runs in seconds, so it is also the fast feedback loop while iterating on index shape. |

Neither substitutes for the other, and a load delta alone would systematically understate index write
cost: empty tables mean shallow trees and sequential appends.

`ingest` draws its resources from the loaded corpus rather than generating them, so it needs no
resource source of its own. The 2026-09-27 design justified `ingest` as a corrective for the
`SqlBulkCopy` seeding path; that path is gone, and the command now stands on the empty-versus-full
distinction instead.

## 11. `CorpusProfiler` — recovering the hot/cold guarantee

The 2026-09-27 design's distribution table existed to guarantee every search parameter had a
deliberate hot and a deliberate cold value, so both ends of the selectivity range were measured
rather than one accidental point. Synthea's skew is real but unknown, so that guarantee must be
**discovered** rather than asserted.

After load, `CorpusProfiler` queries the real index tables and emits `assets/perf/corpus-profile.json`.
Per table it profiles what that table's predicates actually filter on:

| Table | Profiled |
|---|---|
| `IndexToken` | `(SearchParameterStoreId, System, Code)` frequency, ranked |
| `IndexString` | value frequency **and prefix frequency at 1–5 characters** — string search is prefix-based, so prefix selectivity is the number that matters, not whole-value selectivity |
| `IndexDateTime` | min/max and decile boundaries of `Low`/`High`, so a range can be placed at a known selectivity |
| `IndexQuantity` | value deciles per `(code, system)` |
| `IndexReference` | fan-out distribution — referrers per target, so both a 1-referrer and a 1000-referrer target are reachable |

### 11.1 Query values are declared by selectivity, not by literal

A query-set entry says "a value matching ≈20% of this resource type" or "≈0.01%", and the profiler
resolves it to the nearest actual value. Selectivity is what §2.2d is about, so expressing the query
set in those terms says what it means — and it survives being pointed at a different Synthea export,
where rank 1 would be a different code but "≈20% selective" would still mean the same thing.

**Three bands, not two:** **hot** (>10% of type), **mid** (≈1%), **cold** (<0.01%). Two points can
only show that a plan flip exists; they cannot locate it.

**And for `Observation.code` — the single parameter the index-direction question hangs on — a
selectivity sweep:** a dozen values spanning the range, to find the point where the optimiser stops
driving index-table-first and starts driving `ResourceStore`-first. That crossover *is* the answer to
§2.2d, and it is what the 2026-09-27 design could assert but not pin down. The sweep is confined to
one or two parameters deliberately; applied to all of them it would be a combinatorial explosion for
no extra insight.

Every baseline entry records the role, the literal it resolved to, **and the achieved selectivity**.
A baseline must stay readable a year later without the profile file beside it.

## 12. Measurement

The harness measures the SQL **Pyro actually emits**, never hand-written SQL. `QueryRunner` resolves
a real `IResourceStoreSearch` through DI, drives it from a FHIR query string, and captures the
generated command with an EF command interceptor. `PlanAnalyser` then re-executes that captured SQL
with `SET STATISTICS IO, XML ON`.

- **Logical reads — the primary metric.** Hardware-independent, captured per table via
  `SqlConnection.InfoMessage`.
- **Plan operators** — extracted from the plan XML, so "seek, not scan, on index *X*" is a mechanical
  assertion rather than a human reading a plan by eye.
- **Elapsed and CPU — secondary.** N iterations, the first discarded for compilation; median and p95
  reported.
- **Cold and warm both reported** (`CHECKPOINT; DBCC DROPCLEANBUFFERS` for cold).
- **Buffer pressure** is a configurable container memory cap (§5.2).

`IndexShapeProbe` runs hand-written SQL directly against candidate index shapes, so six column orders
can be compared in minutes without touching C#. It is explicitly a **hypothesis generator, never the
evidence of record** — the pipeline-driven measurement is the source of truth, because only it reflects
the SQL EF actually produces.

### 12.1 Result cardinality is part of the baseline

Every baseline entry records **the row count the query returned**, alongside its metrics. Sub-project
B2 deliberately changes result sets — the default string search narrows — and D may change them if a
drop or collation change is ever mis-specified. Without recorded cardinality, a changed result set
reads as a performance delta and the programme would draw exactly the wrong conclusion.

B1 lands before the first baseline is taken (§4.1), so `:missing` is already correct when measured.
Cardinality is what lets any *later* result-set change be seen as a result-set change rather than
misread as a performance one.

## 13. First deliverable

`load` prints, by default and without a flag, real `sp_spaceused` figures per table alongside the
resulting resource count and per-type census, plus measured load wall-clock and throughput. This is
not opt-in: a load that does not report what it produced is a load whose result nobody can check.

This establishes, as measured fact before any index design is committed to: how large the corpus
actually is on disk, how many index rows each table holds, how long a load takes, and how long
`snapshot` and `reset` take. The 2026-09-27 design carried estimates for most of these; this document
carries none (§5.1), and §19 sets no wall-clock target for load because there is no honest basis for
one yet.

## 14. Query set

One entry per access pattern, each at hot, mid and cold selectivity (§11.1):

- **String**: prefix (default), `:exact`, `:contains`
- **Token**: code-only, system+code, system-only, `:not` — plus the `Observation.code` selectivity
  sweep (§11.1)
- **`:missing`**: on string, token, reference, date and quantity — measured only after B1 has landed,
  so these entries record real cardinality rather than zero (§4.1)
- **Reference**: direct, and the multi-id `IN` fast path
- **Chained** (`Observation?subject.name=`) and **`_has`** (`Patient?_has:Observation:subject:code=`)
- **Date**: `eq`, `ge`/`le` range, `gt`/`lt`
- **Quantity** with code
- **Paging**: `_count` at page 1 and at a deep page — genuinely deep, against 259,929 Observations
- **`_include`**
- **The count-versus-page pair** that exposes the double execution (§2.2c) — now measured against a
  ~260,000-row count

The corpus supplies reference-dense resource types the 2026-09-27 generator would not have produced —
`Claim`, `ExplanationOfBenefit`, `Procedure`, `Condition`, `MedicationRequest` — so chained, `_has`
and `_include` are exercised against genuinely messy resources rather than tidy synthetic ones.

### 14.1 Coverage ledger

Stated as a first-class section, because the corpus choice buys realism at the cost of coverage and
sub-project D must not mistake silence for evidence.

| Index table | Evidence from sub-project A |
|---|---|
| `IndexString` | **Full** — real name distributions, with prefix profiling |
| `IndexToken` | **Full** — LOINC/SNOMED codes, strongly skewed, sweep-capable |
| `IndexReference` | **Good for `subject`/`patient`** (≈220:1 fan-out). **Degraded for `Organization`/`Practitioner`** — see below |
| `IndexDateTime` | **Full** — real clinical date clustering |
| `IndexQuantity` | **Full** — 85% of sampled Observations carry `valueQuantity` |
| `IndexUri` | **None** — the corpus populates no uri-type search parameter |
| `IndexPosition` | **None** — by decision; the corpus has no `Location` and no `position` |

**So A produces measured evidence for five of the seven index tables.** Sub-project D must decide
`IndexUri` and `IndexPosition` on structural reasoning, or commission purpose-built data at that time.

Four specifics behind that table:

- **`IndexPosition` / `near`.** The corpus contains zero `Location` resources and no `position`
  element anywhere. `near` is therefore dropped from A's remit entirely. This costs least of the
  three gaps: `near` is the one search type whose index design is already structurally settled — it
  must be a spatial index, and there is no column-order question to answer (§2.2e).
- **Pure number search is dropped from the query set.** The 2026-09-27 design's §12 carried a
  "pure number" entry. R4's number-type search parameters sit on resource types this corpus does not
  contain, so no number-type parameter is populated. Number predicates write into `IndexQuantity`
  rather than a table of their own, so the table itself is still covered (§2.2a names
  `IndexNumberPredicateFactory` as a factory, not a table) — but the number *predicate path* is not
  measured, and sub-project D should treat it as unmeasured.
- **`IndexUri`.** Zero `meta.profile` across 10,218 sampled resources, and no `url`,
  `instantiatesCanonical` or `implicitRules` elements anywhere. R4's uri-type search parameters point
  at `_profile` and at canonical `url` elements on conformance resources, none of which this corpus
  contains. A realistic clinical corpus populating *zero* `IndexUri` rows is itself a data point about
  that table's cost-benefit — but Synthea is one workload class, not all of them, so it is a flag for
  D, not a conclusion.
- **Organization and Practitioner fan-out is artificially flat.** The bundles use no `ifNoneExist`, so
  each one POSTs its own `Organization` and `Practitioner` entries: a few dozen real hospitals become
  2,979 duplicate resources. `Encounter`→`Organization` fan-out measured here is therefore not
  representative. `subject`/`patient` fan-out is unaffected and excellent.

## 15. Reproducibility

Scoped to what is actually true, given §10.1:

- **Within one restored snapshot:** logical reads reproduce **exactly** — they are deterministic.
  Elapsed medians reproduce within **±15%**, a starting figure to be tightened or loosened once the
  first few runs show real machine-to-machine variance.
- **Across a `load --force` reload of the same directory:** row counts, per-type census, resolved
  query literals and plan operators are comparable. Logical reads are comparable within noise but
  **not identical**, because id values and physical row placement differ. Deep paging by
  `LastUpdatedUtc DESC` returns a different page of resources entirely.

Which is the whole reason `reset` is the normal path and reload is the deliberate one (§10.1).

Every baseline entry records the corpus identity hash and whether it was taken against the original
load or a restored snapshot, so no comparison can silently cross that boundary.

## 16. Failure modes

Silent wrongness is the enemy; a harness that quietly measures the wrong thing is worse than no
harness.

| Condition | Behaviour |
|---|---|
| Corpus path not configured | **Refuse to run.** No default inside the repository tree (§5.3). |
| Corpus identity hash does not match the manifest | **Refuse to run**; print the `load` command. Never measure a corpus that is not the one requested. |
| A file is not a `type: transaction` Bundle | Fail loudly, naming the file. |
| Endpoint policy forbids a resource type present in the corpus | Fail **before** loading begins, naming the type and the policy (§9). |
| Database mutated since the snapshot (`ResourceStore` count differs) | **`run` refuses**; instruct `reset` (§10.5). |
| `load` against a non-empty database without `--force` | Refuse. |
| `SET SINGLE_USER` cannot acquire the database | Fail with the connection-drain cause named (§10.2), not a bare SQL error. |
| Plan XML fails to parse | Fail loudly. Never record a null or defaulted metric. |
| A query returns zero rows | Flag as suspicious in the report — a 0-row query's timings are meaningless. |
| Container or migration failure | Clear message naming the cause. |

## 17. Testing the harness

Database-free tests, which are what CI runs (§7):

- `PlanAnalyser` parsing, against committed sample plan XML.
- Corpus identity hashing: a known file list produces a known hash; a changed size changes it.
- `CorpusProfiler`'s selectivity-band and percentile arithmetic, against synthetic frequency tables.
- Query-set role resolution: a role plus a profile resolves to the expected literal, and an
  unsatisfiable role (no value in the corpus near the requested selectivity) fails loudly rather than
  silently picking the nearest thing.

Database tests, developer-local:

- A small-N load (`--max-files 3` — the first three in sorted order, whatever their size) runs end to
  end, writes a manifest, and the resulting index row counts match what the real create path produces
  for the same resources.
- `snapshot` → `ingest` → `reset` returns `ResourceStore` to the manifest's recorded count.
- `run` against a mutated database refuses.

## 18. Out of scope

Any index change; any predicate, handler or pipeline change; `near` and `IndexPosition`; `IndexUri`;
a CI performance gate; a corpus generator; `_include` optimisation; multi-tenant performance; and
measurement against production or Azure. These belong to B1, B2, C, D, E, or to a later decision —
and B1 will already have landed before A begins (§4.1).

## 19. Success criteria

1. `load --corpus <path>` completes against all 1,180 bundles and reports measured per-table sizes,
   resource count, per-type census, wall-clock and throughput (§13). **No wall-clock target** — this
   run establishes the figure.
2. `snapshot` then `reset` returns the database to a state whose `ResourceStore` count and corpus
   identity hash match the manifest, and both operations' durations are recorded.
3. `profile` produces a committed `corpus-profile.json` from which every query-set role in §14
   resolves, with no unsatisfiable roles.
4. `run --queries all` produces a committed baseline covering every pattern in §14, each entry
   carrying logical reads, plan operator, elapsed median/p95, **result cardinality**, and **the
   resolved literal with its achieved selectivity**.
5. `compare` produces a readable markdown delta between two baselines, and refuses to compare across
   different corpus identity hashes.
6. Re-running `run` against a restored snapshot reproduces logical reads **exactly** and elapsed
   medians within **±15%** (§15).
7. Every failure mode in §16 is demonstrated to refuse rather than proceed — in particular, a corpus
   identity mismatch and a post-`ingest` mutation.
8. **§2.2d is answerable.** The `Observation.code` selectivity sweep either locates the point where
   the optimiser switches driving direction, or demonstrates that no such switch occurs across the
   selectivity range the corpus offers. Either result is a result.
9. The coverage ledger (§14.1) is published as part of the baseline output, so no later sub-project
   can mistake an unmeasured table for a measured one.

## 20. Open items

None blocking. Three to settle during implementation planning:

1. **Whether `compare` should fail non-zero on regression.** With the CI gate gone the question is
   narrower than it was: recommend report-only, since the only consumer is a developer reading the
   markdown.
2. **How `reset` interacts with `CorpusHost`'s migration step.** A restored `.bak` already carries the
   schema and `__EFMigrationsHistory` as of load time. If a migration has been added since — which is
   exactly what sub-project D does — `reset` restores an older schema and the harness must then apply
   the pending migrations, or refuse. Recommend: apply, and record in the baseline that the
   measurement ran on a migrated-after-restore database.
3. **Whether `load` should be resumable.** A load of unknown duration that fails on bundle 900 is
   painful. Resumption is feasible — the manifest records progress and the file order is fixed — but
   it is extra machinery for a once-per-index-change operation. Defer until §13 reports how long a
   load actually takes.

# FHIR Search Performance — Programme Design and Sub-Project A (Measurement Harness)

> Date: 2026-09-27 · Status: **Approved design, ready for implementation planning** ·
> Supersedes the reasoning in `assets/plans/fhir-index-plan.md` (that document remains the origin
> of much of Part I's index design thinking, which is carried forward rather than discarded).

This document has two parts. **Part I** records the programme: the review of the prior plan, the
decomposition into sub-projects, and the sequencing decisions. **Part II** is the design for
sub-project A, the measurement harness, which is the only part being built now.

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
| Working data scale | 250k resources (~6.3M index rows) |
| Read/write posture | Aggressive — drop subsumed indexes *and* question the column design itself |
| Re-index capability | In scope, as a first-class server operation |
| Corpus | Deterministic generator only; Synthea considered and dropped |
| Programme structure | Strictly sequential, harness first |

Constraints derived from the repository rather than stated by the owner:

- **Azure SQL, with the app holding only `db_datareader`/`db_datawriter`.** The application cannot
  alter its own schema, so every schema change ships as an EF migration through the CD `migrate`
  job, which runs as `pyro_migration_user`. There is no path for the running server to create an
  index.
- **EF model snapshots must stay byte-identical across Windows and Linux** (`CLAUDE.md`), because
  snapshots are authored on Windows and verified on the Linux CI/CD runner.
- **`IndexPosition` does not follow the other six tables' rules.** It is served by a spatial index,
  whose access pattern is structurally different (see §3.5).

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
  `mcr.microsoft.com/mssql/server:2022-latest` image used by the integration tests — is sound and
  is adopted unchanged.

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
`Special` + `Missing` case to it. Sub-project B is therefore "generalise the `near` pattern to the
other search types", which is cheaper and lower-risk than the prior plan's framing.

**c. The prior plan misses the double execution of the search predicate.**
`ResourceStoreSearch.GetSearch` runs `CountAsync()` (`:56`) and then the paged query (`:67`) —
the full, expensive predicate twice per search. The `COUNT` cannot be short-circuited by paging,
so it touches every matching row while the page touches `_count` rows. For large result sets this
is plausibly the single largest available win. FHIR's `_total` parameter provides the standard
licence to make it optional.

**d. The prior plan asserts an index direction it should be measuring.** Its proposed
`(SearchParameterStoreId, Value) INCLUDE (ResourceStoreId)` only pays off if the optimiser drives
*index-table-first*. For a non-selective value against a selective resource type (for example
`Patient?gender=male`), driving *`ResourceStore`-first* is cheaper and wants the correlation column
early — `(SearchParameterStoreId, ResourceStoreId, Value)`. Both shapes are defensible. Which wins
is a measurement question, and answering it is a primary purpose of sub-project A.

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
| **A** | **Measurement harness** — generator, seeding, query set, plans, baselines | — | fully |
| **B** | Search correctness — `:missing` → `NOT EXISTS`, `:not`, remove string `EndsWith` | — | fully |
| **C** | Re-index operation — re-run setters over stored JSON, scoped by type/parameter | — | fully (benefits from A) |
| **D** | Index and column redesign across the eight tables | A, B, C | no |
| **E** | Query shape — the double `CountAsync`, `_total` support | A | fully |

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

**Approved order: A → B → (C ∥ E) → D.** Correctness precedes index design; index design comes
last, with full evidence.

## 5. Scale and storage

### 5.1 Chosen scales

`250k` resources is the working corpus, not `1M`. The decisions this programme needs are
*plan-shape* decisions — seek versus scan, which table the optimiser drives from, which column
order wins. Those are driven by row count and selectivity, and they stabilise well before a million
resources. At 250k there are ~6.3M index rows, comfortably past the point where a scan is ruinous.
Going to 1M largely multiplies wall-clock without changing which index wins.

| Resources | Index rows | Data files | `.bak` | Peak disk | Seed time | Restore |
|---|---|---|---|---|---|---|
| 10k | 250k | ~70 MB | ~30 MB | ~130 MB | ~10 s | ~2 s |
| **250k** | **6.3M** | **~1.75 GB** | **~0.8 GB** | **~3.3 GB** | **~3 min** | **~20 s** |
| 750k | 18.8M | ~5.25 GB | ~2.4 GB | ~10 GB | ~12 min | ~50 s |

These are **estimates**, derived from schema column widths and index counts (facts) combined with
two assumptions: ~400 bytes average GZip-compressed JSON per resource, and ~25 index rows per
resource. Replacing them with measured figures is the harness's first deliverable (§11).

- **250k — working corpus.** Fast enough to iterate on index shape repeatedly.
- **10k — CI smoke gate.** Seeds in seconds; asserts seek-not-scan only.
- **750k — optional one-off**, run only if a specific decision looks scale-sensitive. Scale stays
  parameterised so this costs nothing but time.

500k and 1M are deliberately skipped: they sit between "iterate cheaply" and "prove it scales"
without serving either.

### 5.2 Buffer pressure is a dial, not a data volume

At 700 MB everything sits in the buffer pool and the I/O cliff never appears. Rather than growing
data to outrun available RAM, the harness **caps the SQL container's memory** — the corpus is sized
for iteration speed and buffer pressure is configured independently.

### 5.3 Storage: store the recipe, not the cake

A generated corpus is a pure function of (generator version, RNG seed, scale, profile). Because
generation is deterministic, the corpus is **reproducible rather than archived**.

| Artifact | Size | Location | In git? |
|---|---|---|---|
| Generator source | ~50–100 KB | `src/Abm.Pyro.Performance/` | **Yes** |
| Corpus manifest (seed, scale, profile, generator version) | <1 KB | `assets/perf/corpus.json` | **Yes** |
| Query set definitions | ~10–20 KB | `assets/perf/queries/` | **Yes** |
| Baseline results | ~100 KB per run | `assets/perf/baselines/` | **Yes** — text, diffs well |
| Seeded database (mdf/ldf) | 1.75 GB | Docker volume | No — outside repo |
| `.bak` snapshot (optional) | 0.8 GB | Outside the repo tree | No |

Total git impact for the whole programme: **under 2 MB**, all of it reviewable text. The baselines
are the most valuable artifact in the repository — every later claim rests on them.

Two safeguards, because scrubbing a multi-gigabyte accidental commit from history is painful:

1. **The corpus cache defaults to a path outside the repository tree**
   (`%LOCALAPPDATA%\Pyro\perf-corpus\`). A file `git add` cannot reach from the repo root cannot be
   committed by accident. `.gitignore` is the second line of defence, not the only one.
2. **Git LFS is not used.** LFS is for large files that genuinely need versioning; regenerable
   derived data is precisely the wrong case for it.

### 5.4 Synthea: considered and dropped

Synthea was initially chosen for realism, then dropped once the storage constraint was clear. It is
the one corpus that cannot be cheaply regenerated — it simulates patient lifetimes, so it is slow to
produce and reproducible only if both tool version and seed are pinned — which makes it the only
artifact needing external storage. For plan-shape decisions, what matters is cardinality and skew,
and the generator controls those *better* than Synthea does, because a distribution can be dialled
to probe a specific index question. Revisit only if a result looks suspiciously synthetic.

---

# Part II — Sub-Project A: Measurement Harness

## 6. Scope

A one-command, reproducible measurement of every FHIR search access pattern against a realistically
skewed 250k-resource corpus, capturing logical reads and actual execution plans, with committed
baselines that every later sub-project is measured against, and surviving as a regression gate.

Sub-project A changes **no** production behaviour. It touches no index, no predicate, no handler.

## 7. Project shape

A new console project `src/Abm.Pyro.Performance` (.NET 10), added to `src/Abm.Pyro.CI.slnf` so CI
compiles it (per `CLAUDE.md`, new .NET 10 projects must be added to the filter).

```
seed      --scale 250000 --seed 42 [--profile default]
run       --queries all --baseline pre-phase-b
compare   --from pre-phase-b --to post-phase-b
ingest    --count 5000            # write-throughput measurement
snapshot  export|import           # optional; writes outside the repo
```

Components, each with a single responsibility and independently testable:

| Component | Responsibility | Touches DB? |
|---|---|---|
| `CorpusGenerator` | `(seed, scale, profile)` → resources. Pure function. | no |
| `DistributionProfile` | The skew definitions. Committed data. | no |
| `BulkIndexWriter` | resource → **real index setters** → `SqlBulkCopy` | yes |
| `CorpusHost` | container lifecycle, migrations, corpus-identity check | yes |
| `QueryRunner` | FHIR query string → real search pipeline → captured SQL | yes |
| `PlanAnalyser` | captured SQL → logical reads + plan operators | yes |
| `IndexShapeProbe` | hand-written SQL against candidate index shapes (§10) | yes |
| `BaselineStore` | baseline JSON read/write | no |
| `ReportWriter` | markdown comparison report | no |

`BulkIndexWriter` is deliberately the seam **sub-project C (re-index) will reuse**: "run the real
setters over a resource and write index rows in bulk" *is* re-indexing. Building it here with that
boundary explicit means C generalises it rather than reimplementing it.

## 8. Corpus generation

### 8.1 Determinism is a hard requirement

The corpus identity is a hash of (generator version, seed, scale, profile), and CI must reproduce
the 10k corpus exactly. Therefore: a fixed-seed RNG threaded explicitly through generation, stable
iteration order, resource ids derived from the seed, and **no `DateTime.Now` and no
`Guid.NewGuid()` anywhere in the generator**.

### 8.2 Distributions

Each distribution exists to probe a specific index decision. Skew, not volume, is what separates a
seek from a scan.

| Distribution | Shape | Index decision it probes |
|---|---|---|
| Family / given names | Zipf — a few very common, long tail | `IndexString` prefix selectivity at both ends |
| Observation codes | ~20 codes cover 80%, tail of ~2,000 | **The index-direction question** (§2.2d) — a hot code and a cold code behave oppositely |
| Token systems | 3–5 systems, heavily skewed to one | whether `System` earns index space at all |
| Dates | clustered recent, with a tail | range predicates; `Low`- versus `High`-leading |
| Observation → Patient references | ~50 observations per patient | `_has`, chained, reference fan-out |
| Location positions | geographic clusters plus outliers | `near`, spatial index behaviour |

Every search parameter receives a deliberately **hot** and **cold** value in the query set, so both
ends are measured rather than one accidental point.

Resource types generated: `Patient`, `Observation`, `Encounter`, `DiagnosticReport`, `Location`.

## 9. Seeding

Resources are generated in memory, the **actual** index setters (`IStringSetter`, `ITokenSetter`,
`IDateTimeSetter`, `IQuantitySetter`, `IReferenceSetter`, `IUriSetter`, `INumberSetter`,
`IPositionSetter`) are run over them, and the results are written with `SqlBulkCopy` into
`ResourceStore` and the seven index tables. Reusing the real setters is what makes the corpus
faithful — indexing semantics are never reimplemented in SQL or in the harness.

**Ingest throughput is measured separately**, by pushing a few thousand resources through the real
`FhirCreateHandler`. The bulk path builds corpora; the handler path measures writes. Conflating them
would let the programme claim a write-cost number the bulk path never exercised — which matters,
because the aggressive index-drop posture must be accountable to a real ingest measurement.

The container is **long-lived by default**: seeded once into a named Docker volume and kept between
runs, rather than disposed as `IntegrationTestFixture` does. `Abm.Pyro.Api.Test`'s fixture keeps its
current disposable, Respawn-per-test behaviour, untouched; the harness has its own persistent host.

## 10. Measurement

The harness measures the SQL **Pyro actually emits**, never hand-written SQL. `QueryRunner` resolves
a real `IResourceStoreSearch` through DI, drives it from a FHIR query string, and captures the
generated command with an EF command interceptor. `PlanAnalyser` then re-executes that captured SQL
with `SET STATISTICS IO, XML ON`.

- **Logical reads — the primary metric.** Hardware-independent, captured per table via
  `SqlConnection.InfoMessage`.
- **Plan operators** — extracted from the plan XML, so "seek, not scan, on index *X*" is a
  mechanical assertion rather than a human reading a plan by eye.
- **Elapsed and CPU — secondary.** N iterations, the first discarded for compilation; median and
  p95 reported.
- **Cold and warm both reported** (`CHECKPOINT; DBCC DROPCLEANBUFFERS` for cold).
- **Buffer pressure** is a configurable container memory cap, defaulting to a value that makes the
  250k corpus not quite fit.

A microbenchmark layer runs hand-written SQL directly against candidate index shapes, so six column
orders can be compared in minutes without touching C#. It is explicitly a **hypothesis generator,
never the evidence of record** — the pipeline-driven macrobenchmark is the source of truth, because
only it reflects the SQL EF actually produces.

### 10.1 Result cardinality is part of the baseline

Every baseline entry records **the row count the query returned**, alongside its metrics.
Sub-project B deliberately changes result sets: `:missing` goes from zero rows (or wrong rows) to
correct rows, and the default string search narrows. Without recorded cardinality, a changed result
set reads as a performance delta and the programme would draw exactly the wrong conclusion.
Cardinality is a first-class field, not a footnote.

## 11. First deliverable

`seed --scale <n> --report`, printing real `sp_spaceused` figures per table. This replaces §5.1's
estimates with measured fact **before** any index design is committed to.

## 12. Query set

One entry per access pattern, each in hot and cold variants:

- **String**: prefix (default), `:exact`, `:contains`
- **Token**: code-only, system+code, system-only, `:not`
- **`:missing`**: on string, token, reference, date, quantity, uri, and `near`
- **Reference**: direct, and the multi-id `IN` fast path
- **Chained** (`subject.name=`) and **`_has`**
- **Date**: `eq`, `ge`/`le` range, `gt`/`lt`
- **Quantity** with code; **pure number**
- **Uri**: `:exact`, `:below`
- **`near`**: with and without distance, `near:missing`, chained `near`
- **Paging**: `_count` at page 1 and at a deep page
- **`_include`**
- **The count-versus-page pair** that exposes the double execution (§2.2c)

## 13. Failure modes

Silent wrongness is the enemy; a harness that quietly measures the wrong thing is worse than no
harness.

| Condition | Behaviour |
|---|---|
| Corpus hash does not match the requested profile | **Refuse to run**; print the reseed command. Never measure a corpus that is not the one requested. |
| Plan XML fails to parse | Fail loudly. Never record a null or defaulted metric. |
| A query returns zero rows | Flag as suspicious in the report — a 0-row query's timings are meaningless, and this is exactly what `:missing` does today. |
| Container or migration failure | Clear message naming the cause. |

## 14. Testing the harness

- `CorpusGenerator` determinism: same seed produces an identical content hash.
- Distribution assertions: the top 20 codes really do cover ~80% of `Observation` rows.
- `PlanAnalyser` parsing, against committed sample plan XML — no database required.
- An integration test that seeds 1k resources, runs one query, and asserts a baseline file is
  produced.
- The 10k CI gate itself, asserting seek-not-scan only, fast enough to live inside `dotnet test`.

## 15. Out of scope

Any index change; any predicate, handler or pipeline change; Synthea; `_include` optimisation;
multi-tenant performance; and measurement against production or Azure. These belong to B, C, D or E.

## 16. Success criteria

1. `seed --scale 250000` completes in roughly 3 minutes and reports measured per-table sizes.
2. `run --queries all` produces a committed baseline covering every pattern in §12, each entry
   carrying logical reads, plan operator, elapsed median/p95, and **result cardinality**.
3. `compare` produces a readable markdown delta between two baselines.
4. Re-running `run` against an unchanged corpus reproduces logical-read counts **exactly** (they are
   deterministic), and elapsed medians within **±15%** — a starting figure, to be tightened or
   loosened once the first few runs show the real machine-to-machine variance.
5. A corpus-hash mismatch refuses to run.
6. The 10k CI gate passes and adds no more than the wall-clock budget set per §17.1.
7. The index-direction question of §2.2d is *answerable* by the harness — hot and cold variants of
   the same parameter show measurably different plan choices.

## 17. Open items

None blocking. Two to settle during implementation planning:

1. **Exact CI wall-clock budget** for the 10k gate — to be set from the first measured run rather
   than guessed now.
2. **Whether `compare` should fail non-zero on regression** (making it usable as a gate) or only
   report. Recommend report-only for the 250k corpus and fail-on-regression for the 10k CI gate.

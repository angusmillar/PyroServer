# Handoff — FHIR Search Performance Harness (Sub-Project A)

> Written 2026-10-01 at the end of a design session, so a fresh agent session can resume without
> re-deriving anything. Read this first, then the spec, then the plan.

## Where things stand

**Design and planning are complete and committed. No implementation code has been written yet.**

| | |
|---|---|
| Branch | `development` (clean working tree as of `780361b`) |
| Spec | `docs/superpowers/specs/2026-10-01-fhir-search-perf-harness-design.md` — **approved by the project owner** |
| Plan | `docs/superpowers/plans/2026-10-01-fhir-search-perf-harness.md` — written, self-reviewed, **owner review in progress** |
| Superseded spec | `docs/superpowers/specs/2026-09-27-fhir-search-perf-harness-design.md` — header marks it superseded; retained for the record |
| Obsolete plan | `docs/superpowers/plans/2026-09-29-fhir-search-perf-harness.md` — header carries a ⛔ DO NOT IMPLEMENT banner |

Relevant commits, newest first:

```
780361b docs: implementation plan for the Synthea-backed perf harness
63fce1c docs: revise the perf harness spec to use a local Synthea corpus
061721d docs: implementation plan for the FHIR search measurement harness   <- now obsolete
```

## The one open question

The session ended waiting on the project owner to answer two things:

1. **Does the plan capture what they want?**
2. **Which execution method** — subagent-driven (a fresh subagent per task, a fresh reviewer between
   each) or native (one session implements everything, one whole-branch review at the end)?

**The recommendation already given was subagent-driven**, because the interface chain is long and
load-bearing: `CapturedCommand` → `QueryMetrics` → `BaselineEntry` threads through six tasks, and the
EF-interceptor decision in Task 8 silently constrains Tasks 9 and 12. A wrong call made early would
surface late, after the baselines it corrupts had been committed as the artifact every later
sub-project is measured against.

Do not start implementing before that answer. Per the `superpowers:brainstorming` hard gate, written
spec approval permits writing the plan; it does not permit implementing it.

## What changed in this session, and why

The owner reversed the superseded spec's central corpus decision. The old design built a
deterministic in-repo `CorpusGenerator` and explicitly rejected Synthea (its §5.4). The new design
uses **Synthea FHIR R4 transaction bundles, read from a configurable directory outside the
repository**, loaded once through Pyro's own transaction pipeline, snapshotted, and reset from that
snapshot.

Decisions the owner made during the session, all now recorded in the spec:

| Decision | Choice | Spec |
|---|---|---|
| Corpus | Synthea, local directory, outside git | §1, §5.4 |
| Load path | POST bundles through the real pipeline — **in-process via DI, not HTTP** | §9 |
| Scale | **All 527,113 resources.** One scale, no ladder | §5.1 |
| `near` / `IndexPosition` | **Dropped from sub-project A** — corpus has no `Location`, no `position` | §14.1 |
| CI | **No CI performance gate.** Harness is developer-local | §1, §7 |
| Scale knob | `--max-files`, not `--max-resources` | §8 |
| `ingest` | **Kept**, on the empty-versus-full argument, not its original one | §10.6 |
| Reset | `BACKUP`/`RESTORE`, **not** SQL Server database snapshots | §10.2, §10.3 |

## Facts about the corpus, measured this session

The corpus is **not in the repository** and must not be put there.

- **Location:** `C:\Temp\Pyroserver\performance-testing\synthea_sample_data_fhir_r4_sep2019\fhir`
  (also present as a `.zip` one directory up).
- 1,180 patient transaction bundles, 1.3 GB JSON, **527,113 resources**, 19 resource types.
- Entries per bundle: median 273, mean 447, **max 18,488 in a single 42 MB file**. Three bundles
  exceed 30 MB, eight exceed 10 MB.
- Every entry is `POST`, every `fullUrl` is a `urn:uuid`, there is **no `ifNoneExist`**, and some
  resources carry `#contained` references.
- Observation 49.3%, Claim 11.6%, Encounter 8.9%, ExplanationOfBenefit 8.9%, Procedure 6.9%,
  DiagnosticReport 3.5%, Patient 0.2% (1,180).
- **Zero `Location` resources and zero `position` elements.**
- **Zero `meta.profile`** across 10,218 sampled resources, and no `url` /
  `instantiatesCanonical` / `implicitRules` anywhere — so `IndexUri` will be empty.
- 85% of sampled Observations carry `valueQuantity`.

Consequence, recorded as spec §14.1's coverage ledger: **sub-project A produces measured evidence for
five of the seven index tables.** `IndexUri` and `IndexPosition` get none. Sub-project D must decide
those two on structural reasoning or commission purpose-built data.

## Codebase facts verified this session

Confirmed by reading source, so a fresh session need not re-check:

| Fact | Location |
|---|---|
| Transaction request record is `FhirBatchOrTransactionRequest`, not `FhirTransactionRequest` | `Abm.Pyro.Domain/FhirRequest/FhirBatchOrTransactionRequest.cs` |
| Mediator is **custom**, not MediatR. Dispatch via `IRequestDispatcher.Send<TResponse>(IRequest<TResponse>, CancellationToken)`, scoped | `Abm.Pyro.Application/Dispatcher/RequestDispatcher.cs` |
| Tenant can be set without HTTP: `ITenantService.SetScopedTenant(Tenant)` | `Abm.Pyro.Application/TenantService/TenantService.cs` |
| Single tenant: `Code` "Pyro", `UrlCode` "pyro", `SqlConnectionStringCode` "PyroDb" | `Abm.Pyro.Api/appsettings.json` → `Tenants:TenantList` |
| Resource ids are random — `GuidSupport.NewFhirGuid()` | `Abm.Pyro.Application/FhirHandler/FhirCreateHandler.cs:80` |
| **FHIR profile validation is already off by default** — the seeded `ServiceSetting` row has `"ValidateOnCreate":false,"ValidateOnUpdate":false` | `Abm.Pyro.Domain/ServiceSettings/FhirValidationSettings.cs` |
| `WebApplicationFactory<Program>` works against `Abm.Pyro.Api`; config knobs are `ConnectionStrings:PyroDb`, `ServiceBaseUrl:Url`, `spring:cloud:config:enabled=false`, `spring:cloud:config:failFast=false` | `Abm.Pyro.Api.Test/Fixtures/PyroWebApplicationFactory.cs` |
| Testcontainers + programmatic migrations + Respawn table list to mirror | `Abm.Pyro.Api.Test/Fixtures/IntegrationTestFixture.cs` |
| `IndexDateTime` columns are `LowUtc` / `HighUtc`, **not** `Low` / `High` | `Abm.Pyro.Domain/Model/IndexDateTime.cs` |
| `SearchParameterStoreId` and `ResourceStoreId` live on `IndexBase`, not the leaf entities | `Abm.Pyro.Domain/Model/IndexBase.cs` |
| Search pipeline is `ISearchQueryService.Process(FhirResourceTypeId, string?)` → `IResourceStoreSearch.GetSearch(SearchQueryServiceOutcome)` / `.GetSearchTotalCount(...)` | `Abm.Pyro.Application/SearchQuery/ISearchQueryService.cs`, `Abm.Pyro.Domain/Query/IResourceStoreSearch.cs` |
| `SearchQueryServiceOutcome.HasInvalidQuery` / `.HasUnsupportedQuery` are the guards a query runner must check | `Abm.Pyro.Domain/SearchQuery/SearchQueryServiceOutcome.cs` |

**A correction the plan records:** spec §9's precondition 2 reads as though the harness must turn
profile validation off. It is already off. The plan converts it to an assertion. The caveat still
stands and still belongs on every write-cost figure the harness reports — measurements exclude
profile-validation cost.

## The six places the plan could not fully verify

Each is flagged inline in the plan as an implementer note, with what to check and what the
requirement is independent of mechanism:

1. **Attaching the EF interceptor to the real `DbContext` registration** (Task 8) — **highest risk.**
   Tasks 9 and 12 both depend on it, and it is the one point where "no production file is modified"
   and "the interceptor sees the real DI graph" pull against each other.
2. `NotificationManager`'s namespace and registration shape (Task 3).
3. Testcontainers 4.15 member names for the memory cap and reuse (Task 3).
4. Whether `PyroDbContext` resolves directly from DI or only via `IPyroDbContextFactory` (Task 3).
5. `InvalidQueryParameter`'s raw-text property name (Task 8).
6. `sys.allocation_units.data_pages` availability on SQL Server 2022 (Task 5).

## Deliberate design choices a fresh session might otherwise "fix"

Do not undo these without talking to the owner — each was argued through:

- **`reset` from snapshot is the normal path; reload is the deliberate one.** Because
  `NewFhirGuid()` and wall-clock `LastUpdatedUtc` mean two loads of the same directory are
  genuinely different databases. The snapshot is what makes a baseline comparable later, not an
  optimisation (spec §10.1).
- **SQL Server database snapshots are rejected**, despite looking faster. A live snapshot makes every
  write to the source do a copy-on-write page push, taxing exactly the writes `ingest` exists to
  measure (spec §10.3).
- **The clock is *not* swapped** in the performance `WebApplicationFactory`, unlike
  `Abm.Pyro.Api.Test`'s. The harness wants the production clock, because `LastUpdatedUtc` is what
  deep paging orders by.
- **`--max-files` defaults to every `*.json` found**, not the literal 1,180. The numbers 1,180 and
  527,113 are measured facts about one export and must never become constants in code (spec §8).
- **Query values are declared by selectivity, not by literal**, resolved from the profile at run
  time (spec §11.1). A role no value satisfies must fail loudly. **Do not widen
  `ValueRoleResolver.ToleranceFactor` to make a role resolve** — that hides exactly what the check
  exists to surface.
- **Three selectivity bands, not two**, plus a 12-point sweep on `Observation.code`. Two points can
  show a plan flip exists but cannot locate it, and locating it is the answer to spec §2.2d.
- **`compare` reports only and always exits 0** on a regression. There is no CI gate to fail and the
  only consumer is a developer reading markdown (spec §20.1).

## Programme context

Sub-project A is one of five. Approved order: **B1 → A → B2 → (C ∥ E) → D**.

**B1 — fixing `:missing` and `:not` — is supposed to land before A begins** (spec §4.1), because
`:missing` currently returns zero rows or wrong rows silently, and baselining a broken query would
record "0 rows, instant" that then reads as a catastrophic regression once fixed. Check whether B1
has shipped before taking the first baseline. If it has not, that is a sequencing decision for the
owner, not something to work around.

One knock-on worth knowing: the superseded design justified a `BulkIndexWriter` in A as the seam
sub-project C (re-index) would reuse. **That component no longer exists, so C inherits no bulk-write
seam** (spec §3).

## How to resume

1. Read `docs/superpowers/specs/2026-10-01-fhir-search-perf-harness-design.md` (Part II is the one
   being built; Part I is programme context).
2. Read `docs/superpowers/plans/2026-10-01-fhir-search-perf-harness.md`.
3. Ask the owner the two open questions above — plan approval, and execution method.
4. On approval, invoke `superpowers:subagent-driven-development` or
   `superpowers:executing-plans` per their choice, and work Task 1 onward.
5. Docker must be running from Task 3 onward.

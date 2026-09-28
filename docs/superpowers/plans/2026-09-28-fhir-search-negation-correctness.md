# FHIR Search Negation Correctness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `:missing`, `:not` and the `ne` prefix return spec-correct FHIR R4 result sets by lifting negation out of the `EXISTS` subquery over index tables and up to the `ResourceStore` level.

**Architecture:** Each index predicate factory currently returns `List<Expression<Func<IndexX, bool>>>`, which its single caller wraps in `.Any(...)`. That shape cannot express negation, which is the root cause of every defect here. Each factory changes to return a fully-folded `Expression<Func<ResourceStore, bool>>` — the shape `IndexPositionPredicateFactory.PositionIndexMissing` already uses — building its terms and folding them through one shared internal helper. `ISearchPredicateFactory.GetResourceStoreIndexPredicate` keeps its signature, so composite, chained and `_has` need no changes at all.

**Tech Stack:** .NET 10, EF Core 10.0.12 (SQL Server), LinqKit `PredicateBuilder`/`AsExpandable`, xUnit 2.9.3, Moq 4.20.72, Testcontainers (integration tests), `Hl7.Fhir.R4` 6.5.0.

**Spec:** [docs/superpowers/specs/2026-09-28-fhir-search-negation-correctness-design.md](../specs/2026-09-28-fhir-search-negation-correctness-design.md)

## Global Constraints

- Target framework `net10.0`, `LangVersion 14`, `Nullable enable` — matching the existing test projects.
- Assertions use plain xUnit `Assert.*`. There is no FluentAssertions or Shouldly in this solution; do not add one.
- Mocking uses `Moq` 4.20.72 where a unit test needs a collaborator.
- Files needing the FHIR model namespace and/or `Task` use `using Hl7.Fhir.Model;` plus `using Task = System.Threading.Tasks.Task;` — the alias avoids collision with the FHIR `Task` resource.
- Integration tests inherit `IntegrationTestBase(fixture)`, which resets the database per test via Respawn and builds a `FhirClient` at base address `pyro/`.
- **No schema change, no EF migration, no index change.** If a step seems to require one, stop — it is out of scope.
- `ISearchPredicateFactory.GetResourceStoreIndexPredicate` must keep its exact signature: `Task<ExpressionStarter<ResourceStore>> GetResourceStoreIndexPredicate(IEnumerable<SearchQueryBase> searchQueryList)`.
- Every integration test asserts **exact membership** (which resources came back), never only a count. A bug returning the right number of wrong rows is exactly what already happened here.
- `LinqKit` `.Compile()` inside a predicate is a marker expanded by `AsExpandable()` at query time. It is never executed in memory. Keep it.
- Run the full suite with `dotnet test src/Abm.Pyro.CI.slnf`. Integration tests need Docker running.

### Deviation from the spec, deliberate

Spec §4.2 sketches `IndexPredicateComposer.Compose` taking an `Expression<Func<ResourceStore, IEnumerable<TIndex>>> navigation`. This plan instead passes a **wrap delegate**. Building `x => !navigation.Invoke(x).Any(p.Compile())` nests three LinqKit markers (`Invoke` inside `Any` inside `!`), which is an avoidable translation risk when the literal form `x => !x.IndexStringList.Any(p.Compile())` is already proven in this codebase. The delegate keeps each factory's navigation literal and explicit. Same design, lower risk.

## Review Focus

Five conditions the spec implies but does not give tests for, most likely to bite first:

1. **`:missing=true` must not return deleted or non-current resources.** `NOT EXISTS` is a new interaction with `CurrentMainResource`'s `IsCurrent && !IsDeleted` filter; a resource whose current version is deleted must never appear. → Task 3.
2. **A non-boolean `:missing` value must still return 400.** `name:missing=maybe` sets `InvalidMessage` today; lifting negation must not let it silently degrade to `false`. → Task 3.
3. **Two negated parameters in one query must AND at the outer level.** `?name:missing=true&gender:not=male` exercises composition across parameters, not just within one. → Task 9.
4. **`:not` with a system-only value.** `code:not=http://loinc.org|` is the fourth token form under negation; §5.1 lists it but the spec's test list does not name it. → Task 9.
5. **An index row with a null value component still counts as present.** An `IndexToken` row with `System = null` means `code:missing=false` must match — presence is presence, regardless of which columns are null. → Task 9.

---

## File Structure

**New files**

| Path | Responsibility |
|---|---|
| `src/Abm.Pyro.Repository.Test/Abm.Pyro.Repository.Test.csproj` | xUnit project for Repository-internal unit tests |
| `src/Abm.Pyro.Repository/Predicates/PredicateCombine.cs` | The fold operator enum |
| `src/Abm.Pyro.Repository/Predicates/IndexPredicateTerm.cs` | One index-row predicate plus whether it is negated |
| `src/Abm.Pyro.Repository/Predicates/IndexPredicateComposer.cs` | Folds terms into a `ResourceStore`-level predicate |
| `src/Abm.Pyro.Repository.Test/Predicates/IndexPredicateComposerTests.cs` | Unit tests for the fold |
| `src/Abm.Pyro.Repository.Test/Predicates/IndexFactoryTermShapeTests.cs` | Per-factory `:missing` / `:not` term-shape tests |
| `src/Abm.Pyro.Api.Test/Chaining/ChainedMissingSearchTests.cs` | Chained and `_has` `:missing` coverage |

**Modified files** — every index factory, its interface, the two aggregating types, and the test builders:

| Path | Change |
|---|---|
| `src/Abm.Pyro.Repository/Abm.Pyro.Repository.csproj` | `InternalsVisibleTo` for the new test project |
| `src/Abm.Pyro.CI.slnf`, `src/Abm.Pyro.sln` | register the new test project |
| `Predicates/IIndexStringPredicateFactory.cs` and six siblings | return type → `Expression<Func<ResourceStore, bool>>` |
| `Predicates/IndexStringPredicateFactory.cs` and six siblings | build terms, fold, delete `IsNotSearchParameterId` |
| `Predicates/IIndexPositionPredicateFactory.cs`, `IndexPositionPredicateFactory.cs` | `PositionIndex` absorbs `PositionIndexMissing` |
| `Predicates/IResourceStorePredicateFactory.cs`, `ResourceStorePredicateFactory.cs` | return types; drop `PositionIndexMissing` |
| `Predicates/SearchSearchPredicateFactory.cs` | switch collapses to one line per type |
| `Abm.Pyro.Api.Test/Support/PatientBuilder.cs`, `ObservationBuilder.cs` | absent-value and multi-coding fixtures |
| `Abm.Pyro.Api.Test/Search/*IndexSearchTests.cs` | `:missing` / `:not` / `ne` coverage |

---

### Task 1: Repository test project and the shared composer

**Files:**
- Create: `src/Abm.Pyro.Repository.Test/Abm.Pyro.Repository.Test.csproj`
- Create: `src/Abm.Pyro.Repository/Predicates/PredicateCombine.cs`
- Create: `src/Abm.Pyro.Repository/Predicates/IndexPredicateTerm.cs`
- Create: `src/Abm.Pyro.Repository/Predicates/IndexPredicateComposer.cs`
- Modify: `src/Abm.Pyro.Repository/Abm.Pyro.Repository.csproj`
- Modify: `src/Abm.Pyro.CI.slnf`
- Test: `src/Abm.Pyro.Repository.Test/Predicates/IndexPredicateComposerTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `internal enum PredicateCombine { Or, And }`; `internal sealed record IndexPredicateTerm<TIndex>(Expression<Func<TIndex, bool>> Predicate, bool Negated)`; `internal static Expression<Func<ResourceStore, bool>> IndexPredicateComposer.Compose<TIndex>(IReadOnlyList<IndexPredicateTerm<TIndex>> terms, PredicateCombine combine, Func<Expression<Func<TIndex, bool>>, bool, Expression<Func<ResourceStore, bool>>> wrap)`. Every later task consumes all three.

- [ ] **Step 1: Create the test project and register it**

```bash
cd src
dotnet new xunit -n Abm.Pyro.Repository.Test -o Abm.Pyro.Repository.Test --framework net10.0
dotnet add Abm.Pyro.Repository.Test/Abm.Pyro.Repository.Test.csproj reference Abm.Pyro.Repository/Abm.Pyro.Repository.csproj
dotnet add Abm.Pyro.Repository.Test/Abm.Pyro.Repository.Test.csproj package Moq --version 4.20.72
dotnet sln Abm.Pyro.sln add Abm.Pyro.Repository.Test/Abm.Pyro.Repository.Test.csproj
```

Then set `<LangVersion>14</LangVersion>` and `<Nullable>enable</Nullable>` in the new csproj's `PropertyGroup`, matching `Abm.Pyro.Application.Test`.

Add the project to `src/Abm.Pyro.CI.slnf`'s `projects` array so CI builds it:

```json
      "Abm.Pyro.Repository.Test\\Abm.Pyro.Repository.Test.csproj"
```

- [ ] **Step 2: Expose Repository internals to the test project**

In `src/Abm.Pyro.Repository/Abm.Pyro.Repository.csproj`, add:

```xml
  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>Abm.Pyro.Repository.Test</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
```

- [ ] **Step 3: Write the failing tests for the composer**

Create `src/Abm.Pyro.Repository.Test/Predicates/IndexPredicateComposerTests.cs`:

```csharp
using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Repository.Predicates;

namespace Abm.Pyro.Repository.Test.Predicates;

public class IndexPredicateComposerTests
{
    // A stand-in for a real index entity. The composer never inspects TIndex.
    private sealed record FakeIndex(int SearchParameterStoreId, string Value);

    /// <summary>
    /// The composer never executes the navigation itself, so the tests supply a wrap that
    /// evaluates a term against an in-memory list held on a closure. That lets us assert the
    /// composed predicate's *behaviour* rather than its expression tree shape.
    /// </summary>
    private static Func<Expression<Func<FakeIndex, bool>>, bool, Expression<Func<ResourceStore, bool>>>
        WrapAgainst(IReadOnlyList<FakeIndex> rows) =>
            (predicate, negated) =>
            {
                Func<FakeIndex, bool> compiled = predicate.Compile();
                bool any = rows.Any(compiled);
                bool result = negated ? !any : any;
                return _ => result;
            };

    private static IndexPredicateTerm<FakeIndex> Term(string value, bool negated) =>
        new(x => x.Value == value, negated);

    private static bool Evaluate(Expression<Func<ResourceStore, bool>> predicate) =>
        predicate.Compile()(null!);

    [Fact]
    public void Compose_Or_TwoPositiveTerms_MatchesWhenEitherMatches()
    {
        var rows = new List<FakeIndex> { new(1, "smith") };

        Expression<Func<ResourceStore, bool>> predicate = IndexPredicateComposer.Compose(
            [Term("smith", negated: false), Term("jones", negated: false)],
            PredicateCombine.Or,
            WrapAgainst(rows));

        Assert.True(Evaluate(predicate));
    }

    [Fact]
    public void Compose_Or_NegatedTerm_MatchesWhenNoRowMatches()
    {
        var rows = new List<FakeIndex>();

        Expression<Func<ResourceStore, bool>> predicate = IndexPredicateComposer.Compose(
            [Term("smith", negated: true)],
            PredicateCombine.Or,
            WrapAgainst(rows));

        Assert.True(Evaluate(predicate));
    }

    [Fact]
    public void Compose_Or_NegatedAndPositiveTerm_MatchesEverything()
    {
        // This is the near:missing=true,false case: "absent OR present" is a tautology,
        // and it must stay one. An earlier design folded negated terms with And, which
        // turned this into "absent AND present" -- always false. Do not regress it.
        var withRow = new List<FakeIndex> { new(1, "anything") };
        var withoutRow = new List<FakeIndex>();

        Expression<Func<ResourceStore, bool>> whenPresent = IndexPredicateComposer.Compose(
            [Term("anything", negated: true), Term("anything", negated: false)],
            PredicateCombine.Or,
            WrapAgainst(withRow));

        Expression<Func<ResourceStore, bool>> whenAbsent = IndexPredicateComposer.Compose(
            [Term("anything", negated: true), Term("anything", negated: false)],
            PredicateCombine.Or,
            WrapAgainst(withoutRow));

        Assert.True(Evaluate(whenPresent));
        Assert.True(Evaluate(whenAbsent));
    }

    [Fact]
    public void Compose_And_TwoNegatedTerms_ExcludesWhenEitherMatches()
    {
        // gender:not=male,female must exclude a resource that has either value.
        var rows = new List<FakeIndex> { new(1, "male") };

        Expression<Func<ResourceStore, bool>> predicate = IndexPredicateComposer.Compose(
            [Term("male", negated: true), Term("female", negated: true)],
            PredicateCombine.And,
            WrapAgainst(rows));

        Assert.False(Evaluate(predicate));
    }

    [Fact]
    public void Compose_And_TwoNegatedTerms_MatchesWhenNeitherMatches()
    {
        var rows = new List<FakeIndex> { new(1, "other") };

        Expression<Func<ResourceStore, bool>> predicate = IndexPredicateComposer.Compose(
            [Term("male", negated: true), Term("female", negated: true)],
            PredicateCombine.And,
            WrapAgainst(rows));

        Assert.True(Evaluate(predicate));
    }

    [Fact]
    public void Compose_EmptyTermList_Throws()
    {
        Assert.Throws<ArgumentException>(() => IndexPredicateComposer.Compose(
            Array.Empty<IndexPredicateTerm<FakeIndex>>(),
            PredicateCombine.Or,
            WrapAgainst([])));
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test src/Abm.Pyro.Repository.Test/Abm.Pyro.Repository.Test.csproj`
Expected: compile failure — `PredicateCombine`, `IndexPredicateTerm` and `IndexPredicateComposer` do not exist.

- [ ] **Step 5: Create the three types**

`src/Abm.Pyro.Repository/Predicates/PredicateCombine.cs`:

```csharp
namespace Abm.Pyro.Repository.Predicates;

/// <summary>
/// How a search parameter's index predicate terms fold together.
/// FHIR R4 OR's comma-separated values, so <see cref="Or"/> is the default. ':not' folds with
/// <see cref="And"/>, because negating an OR'd value set is, by De Morgan, an AND of negations:
/// 'gender:not=male,female' must exclude both rather than match everything.
/// </summary>
internal enum PredicateCombine
{
    Or,
    And
}
```

`src/Abm.Pyro.Repository/Predicates/IndexPredicateTerm.cs`:

```csharp
using System.Linq.Expressions;

namespace Abm.Pyro.Repository.Predicates;

/// <summary>
/// One index-row predicate, plus whether the resource matches when such a row is ABSENT.
/// Negation lives here rather than inside <paramref name="Predicate"/> because an index-row
/// predicate can only assert "a row exists matching X", never "no row matches X" -- and
/// inverting the comparison inside the row test is satisfied by any sibling row, which is the
/// bug this design removes.
/// </summary>
internal sealed record IndexPredicateTerm<TIndex>(
    Expression<Func<TIndex, bool>> Predicate,
    bool Negated);
```

`src/Abm.Pyro.Repository/Predicates/IndexPredicateComposer.cs`:

```csharp
using System.Linq.Expressions;
using LinqKit;
using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Repository.Predicates;

internal static class IndexPredicateComposer
{
    /// <summary>
    /// Folds a search parameter's index-row terms into a single ResourceStore-level predicate.
    /// </summary>
    /// <param name="wrap">
    /// Turns one term into a ResourceStore predicate. Each factory supplies its own navigation
    /// literally -- (p, negated) => negated ? x => !x.IndexStringList.Any(p.Compile())
    /// : x => x.IndexStringList.Any(p.Compile()) -- so no LinqKit Invoke marker is nested
    /// inside Any inside a negation.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown on an empty term list. An empty fold has no defensible answer: it collapses to
    /// "matches nothing" for Or and "matches everything" for And, and a search parameter that
    /// produced no terms should never silently become either.
    /// </exception>
    public static Expression<Func<ResourceStore, bool>> Compose<TIndex>(
        IReadOnlyList<IndexPredicateTerm<TIndex>> terms,
        PredicateCombine combine,
        Func<Expression<Func<TIndex, bool>>, bool, Expression<Func<ResourceStore, bool>>> wrap)
    {
        if (terms.Count == 0)
        {
            throw new ArgumentException(
                "A search parameter produced no index predicate terms. An empty fold collapses to " +
                "'matches nothing' (Or) or 'matches everything' (And); neither is a defensible result.",
                nameof(terms));
        }

        // An Or-fold must start from false and an And-fold from true.
        ExpressionStarter<ResourceStore> predicate =
            PredicateBuilder.New<ResourceStore>(combine == PredicateCombine.And);

        foreach (IndexPredicateTerm<TIndex> term in terms)
        {
            Expression<Func<ResourceStore, bool>> termPredicate = wrap(term.Predicate, term.Negated);

            predicate = combine == PredicateCombine.Or
                ? predicate.Or(termPredicate)
                : predicate.And(termPredicate);
        }

        return predicate;
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test src/Abm.Pyro.Repository.Test/Abm.Pyro.Repository.Test.csproj`
Expected: PASS, 6 tests.

- [ ] **Step 7: Verify the solution filter still builds**

Run: `dotnet build src/Abm.Pyro.CI.slnf`
Expected: build succeeds, including `Abm.Pyro.Repository.Test`.

- [ ] **Step 8: Commit**

```bash
git add src/Abm.Pyro.Repository.Test src/Abm.Pyro.Repository/Predicates/PredicateCombine.cs src/Abm.Pyro.Repository/Predicates/IndexPredicateTerm.cs src/Abm.Pyro.Repository/Predicates/IndexPredicateComposer.cs src/Abm.Pyro.Repository/Abm.Pyro.Repository.csproj src/Abm.Pyro.CI.slnf src/Abm.Pyro.sln
git commit -m "feat: add index predicate composer for ResourceStore-level negation"
```

---

### Task 2: Test fixtures for absent values and multiple codings

The existing builders always populate every element, so there is currently no way to create a resource that *lacks* a searchable value — which is exactly what every `:missing` test needs.

**Files:**
- Modify: `src/Abm.Pyro.Api.Test/Support/PatientBuilder.cs`
- Modify: `src/Abm.Pyro.Api.Test/Support/ObservationBuilder.cs`
- Modify: `src/Abm.Pyro.Api.Test/Support/ValueSetBuilder.cs`
- Modify: `src/Abm.Pyro.Api.Test/Support/OrganizationBuilder.cs`
- Modify: `src/Abm.Pyro.Api.Test/Support/CodeSystemUriSupport.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `PatientBuilder.Build(..., bool includeName = true, bool includeBirthDate = true, bool includeGender = true)`; `ObservationBuilder.Build(..., string? snomedCode = null, bool includeCode = true)`; `ValueSetBuilder.Build(..., bool includeUrl = true)`; `OrganizationBuilder.Build(..., bool includeName = true)`; `CodeSystemUriSupport.Snomed`. Tasks 3–12 consume these.

**Why `includeX` flags rather than passing `null`:** every existing builder coalesces a null
argument back to a default (`url ?? "http://example.org/..."`, `name ?? "TestOrganization"`), so
passing `null` produces a resource *with* the value. An explicit flag is the only way to omit an
element, which is what every `:missing` fixture needs.

- [ ] **Step 1: Extend `PatientBuilder`**

Replace the body of `src/Abm.Pyro.Api.Test/Support/PatientBuilder.cs` with:

```csharp
namespace Abm.Pyro.Api.Test.Support;

public static class PatientBuilder
{
    public static Hl7.Fhir.Model.Patient Build(
        string? id = null,
        string? familyName = null,
        string? givenName = null,
        string? deceasedDateTime = null,
        string? managingOrganizationId = null,
        bool includeName = true,
        bool includeBirthDate = true,
        bool includeGender = true)
    {
        var patient = new Hl7.Fhir.Model.Patient
        {
            Id = id
        };

        if (includeName)
        {
            patient.Name =
            [
                new Hl7.Fhir.Model.HumanName
                {
                    Family = familyName ?? "TestFamily",
                    Given = [givenName ?? "TestGiven"]
                }
            ];
        }

        if (includeBirthDate)
        {
            patient.BirthDate = "1990-01-15";
        }

        if (includeGender)
        {
            patient.Gender = Hl7.Fhir.Model.AdministrativeGender.Unknown;
        }

        if (deceasedDateTime is not null)
        {
            patient.Deceased = new Hl7.Fhir.Model.FhirDateTime(deceasedDateTime);
        }

        if (managingOrganizationId is not null)
        {
            patient.ManagingOrganization = new Hl7.Fhir.Model.ResourceReference($"Organization/{managingOrganizationId}");
        }

        return patient;
    }
}
```

- [ ] **Step 2: Extend `ObservationBuilder`**

In `src/Abm.Pyro.Api.Test/Support/ObservationBuilder.cs`, add a `Snomed` constant to `CodeSystemUriSupport` first:

```csharp
namespace Abm.Pyro.Api.Test.Support;

public static class CodeSystemUriSupport
{
    public const string Loinc = "http://loinc.org";
    public const string Ucum = "http://unitsofmeasure.org";
    public const string Snomed = "http://snomed.info/sct";
}
```

Then replace `ObservationBuilder.Build`'s signature and code construction:

```csharp
    public static Hl7.Fhir.Model.Observation Build(
        string? subjectPatientId = null,
        string? loincCode = null,
        decimal? valueQuantityAmount = null,
        string? valueQuantityUnit = null,
        string? snomedCode = null,
        bool includeCode = true)
    {
        var observation = new Hl7.Fhir.Model.Observation
        {
            Status = Hl7.Fhir.Model.ObservationStatus.Final
        };

        if (includeCode)
        {
            var coding = new List<Hl7.Fhir.Model.Coding>
            {
                new()
                {
                    System = CodeSystemUriSupport.Loinc,
                    Code = loincCode ?? "29463-7",
                    Display = "Body weight"
                }
            };

            if (snomedCode is not null)
            {
                coding.Add(new Hl7.Fhir.Model.Coding
                {
                    System = CodeSystemUriSupport.Snomed,
                    Code = snomedCode
                });
            }

            observation.Code = new Hl7.Fhir.Model.CodeableConcept { Coding = coding };
        }
```

Leave the rest of the method (subject, value) unchanged.

- [ ] **Step 3: Allow `ValueSetBuilder` to omit the url**

Replace `src/Abm.Pyro.Api.Test/Support/ValueSetBuilder.cs`:

```csharp
namespace Abm.Pyro.Api.Test.Support;

public static class ValueSetBuilder
{
    public static Hl7.Fhir.Model.ValueSet Build(
        string? url = null,
        string? name = null,
        bool includeUrl = true)
    {
        return new Hl7.Fhir.Model.ValueSet
        {
            Url = includeUrl ? url ?? "http://example.org/fhir/ValueSet/test-valueset" : null,
            Name = name ?? "TestValueSet",
            Status = Hl7.Fhir.Model.PublicationStatus.Draft
        };
    }
}
```

- [ ] **Step 4: Allow `OrganizationBuilder` to omit the name**

Replace `src/Abm.Pyro.Api.Test/Support/OrganizationBuilder.cs`:

```csharp
namespace Abm.Pyro.Api.Test.Support;

public static class OrganizationBuilder
{
    public static Hl7.Fhir.Model.Organization Build(
        string? id = null,
        string? name = null,
        bool includeName = true)
    {
        return new Hl7.Fhir.Model.Organization
        {
            Id = id,
            Name = includeName ? name ?? "TestOrganization" : null
        };
    }
}
```

- [ ] **Step 5: Run the existing search tests to confirm nothing regressed**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~Search"`
Expected: PASS. The defaults reproduce the previous behaviour exactly.

- [ ] **Step 6: Commit**

```bash
git add src/Abm.Pyro.Api.Test/Support
git commit -m "test: allow builders to omit searchable values and add a second coding"
```

---

### Task 3: Migrate `IndexString` — the pilot

This task establishes the pattern every later factory follows, and carries Review Focus items 1 and 2.

**Files:**
- Modify: `src/Abm.Pyro.Repository/Predicates/IIndexStringPredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/IndexStringPredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/IResourceStorePredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/ResourceStorePredicateFactory.cs:34-42`
- Modify: `src/Abm.Pyro.Repository/Predicates/SearchSearchPredicateFactory.cs`
- Test: `src/Abm.Pyro.Api.Test/Search/StringIndexSearchTests.cs`

**Interfaces:**
- Consumes: `IndexPredicateComposer.Compose`, `IndexPredicateTerm<TIndex>`, `PredicateCombine` (Task 1); `PatientBuilder.Build(includeName:)` (Task 2).
- Produces: `Expression<Func<ResourceStore, bool>> IIndexStringPredicateFactory.StringIndex(SearchQueryString)` and `IResourceStorePredicateFactory.StringIndex(SearchQueryBase)` with the same return type. Tasks 4–10 copy this shape.

- [ ] **Step 1: Write the failing integration tests**

Append to `src/Abm.Pyro.Api.Test/Search/StringIndexSearchTests.cs`, inside the existing class:

```csharp
    [Fact]
    public async Task Search_NameMissingTrue_ReturnsOnlyPatientWithoutName()
    {
        Patient? withName = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Smith"));
        Patient? withoutName = await FhirClient.CreateAsync(PatientBuilder.Build(includeName: false));
        Assert.NotNull(withName);
        Assert.NotNull(withoutName);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(new[] { "name:missing=true" });

        Assert.NotNull(bundle);
        Assert.Equal([withoutName.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    [Fact]
    public async Task Search_NameMissingFalse_ReturnsOnlyPatientWithName()
    {
        Patient? withName = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Smith"));
        Patient? withoutName = await FhirClient.CreateAsync(PatientBuilder.Build(includeName: false));
        Assert.NotNull(withName);
        Assert.NotNull(withoutName);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(new[] { "name:missing=false" });

        Assert.NotNull(bundle);
        Assert.Equal([withName.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    [Fact]
    public async Task Search_NameMissingTrue_ExcludesDeletedResource()
    {
        // Review Focus 1: NOT EXISTS must compose with the IsCurrent/IsDeleted filter.
        Patient? deleted = await FhirClient.CreateAsync(PatientBuilder.Build(includeName: false));
        Assert.NotNull(deleted);
        await FhirClient.DeleteAsync($"Patient/{deleted.Id}");

        Patient? live = await FhirClient.CreateAsync(PatientBuilder.Build(includeName: false));
        Assert.NotNull(live);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(new[] { "name:missing=true" });

        Assert.NotNull(bundle);
        Assert.Equal([live.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    [Fact]
    public async Task Search_NameMissingWithNonBooleanValue_ReturnsBadRequest()
    {
        // Review Focus 2: a bad :missing value must stay a 400, not degrade to false.
        FhirOperationException exception = await Assert.ThrowsAsync<FhirOperationException>(
            () => FhirClient.SearchAsync<Patient>(new[] { "name:missing=maybe" }));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, exception.Status);
    }
```

Add `using Hl7.Fhir.Rest;` to the file's usings if it is not already present.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~StringIndexSearchTests"`
Expected: the two `:missing` membership tests FAIL — the `missing=true` case returns an empty bundle because the current predicate is `spid = @x AND spid <> @x`. The deleted-resource test also fails (empty bundle). The 400 test may already pass.

- [ ] **Step 3: Change the factory interface**

`src/Abm.Pyro.Repository/Predicates/IIndexStringPredicateFactory.cs`:

```csharp
using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IIndexStringPredicateFactory
{
  Expression<Func<ResourceStore, bool>> StringIndex(SearchQueryString searchQueryString);
}
```

- [ ] **Step 4: Rewrite the factory**

Replace `src/Abm.Pyro.Repository/Predicates/IndexStringPredicateFactory.cs` entirely:

```csharp
using System.Linq.Expressions;
using System.Net;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Exceptions;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;

namespace Abm.Pyro.Repository.Predicates;

public class IndexStringPredicateFactory : IIndexStringPredicateFactory
{
  public Expression<Func<ResourceStore, bool>> StringIndex(SearchQueryString searchQueryString)
  {
    if (!searchQueryString.SearchParameter.SearchParameterStoreId.HasValue)
    {
      throw new ArgumentNullException(nameof(searchQueryString.SearchParameter.SearchParameterStoreId));
    }

    int searchParameterId = searchQueryString.SearchParameter.SearchParameterStoreId.Value;
    var terms = new List<IndexPredicateTerm<IndexString>>();
    PredicateCombine combine = PredicateCombine.Or;

    foreach (SearchQueryStringValue stringValue in searchQueryString.ValueList)
    {
      if (!searchQueryString.Modifier.HasValue)
      {
        if (stringValue.Value is null)
        {
          throw new ArgumentNullException(nameof(stringValue.Value));
        }

        terms.Add(new IndexPredicateTerm<IndexString>(
          And(IsSearchParameterId(searchParameterId), StartsWithOrEndsWith(stringValue.Value)),
          Negated: false));
        continue;
      }

      var arrayOfSupportedModifiers = FhirSearchQuerySupport.GetModifiersForSearchType(searchQueryString.SearchParameter.Type);
      if (!arrayOfSupportedModifiers.Contains(searchQueryString.Modifier.Value))
      {
        throw new FhirFatalException(HttpStatusCode.InternalServerError, $"Internal Server Error: The search query modifier: {searchQueryString.Modifier.Value.GetCode()} is not supported for search parameter types of {searchQueryString.SearchParameter.Type.GetCode()}. ");
      }

      if (searchQueryString.Modifier.Value != SearchModifierCodeId.Missing && stringValue.Value is null)
      {
        throw new ArgumentNullException(nameof(stringValue.Value));
      }

      switch (searchQueryString.Modifier.Value)
      {
        case SearchModifierCodeId.Missing:
          // ':missing=true' asserts absence, ':missing=false' asserts presence. They are two
          // distinct assertions selected by the boolean, not one predicate negated two ways,
          // so they fold with Or like any other comma-separated value list.
          terms.Add(new IndexPredicateTerm<IndexString>(
            IsSearchParameterId(searchParameterId),
            Negated: stringValue.IsMissing));
          break;
        case SearchModifierCodeId.Exact:
          terms.Add(new IndexPredicateTerm<IndexString>(
            And(IsSearchParameterId(searchParameterId), EqualTo(stringValue.Value!)),
            Negated: false));
          break;
        case SearchModifierCodeId.Contains:
          terms.Add(new IndexPredicateTerm<IndexString>(
            And(IsSearchParameterId(searchParameterId), Contains(stringValue.Value!)),
            Negated: false));
          break;
        default:
          throw new FhirFatalException(HttpStatusCode.InternalServerError, $"The search query modifier: {searchQueryString.Modifier.Value.GetCode()} has been added to the supported list for {searchQueryString.SearchParameter.Type.GetCode()} search parameter queries and yet no database predicate has been provided. ");
      }
    }

    return IndexPredicateComposer.Compose(
      terms,
      combine,
      (predicate, negated) => negated
        ? x => !x.IndexStringList.Any(predicate.Compile())
        : x => x.IndexStringList.Any(predicate.Compile()));
  }

  private static Expression<Func<IndexString, bool>> And(
    Expression<Func<IndexString, bool>> left,
    Expression<Func<IndexString, bool>> right)
  {
    return LinqKit.PredicateBuilder.New<IndexString>(true).And(left).And(right);
  }

  private static Expression<Func<IndexString, bool>> IsSearchParameterId(int searchParameterId)
  {
    return x => x.SearchParameterStoreId == searchParameterId;
  }

  private static Expression<Func<IndexString, bool>> StartsWithOrEndsWith(string stringValue)
  {
    return x => (x.Value.StartsWith(stringValue) || x.Value.EndsWith(stringValue));
  }

  private static Expression<Func<IndexString, bool>> EqualTo(string stringValue)
  {
    return x => x.Value.Equals(stringValue);
  }

  private static Expression<Func<IndexString, bool>> Contains(string stringValue)
  {
    return x => x.Value.Contains(stringValue);
  }
}
```

`IsNotSearchParameterId`, `AnyIndex` and `AnyIndexEquals` are gone. `StartsWithOrEndsWith` stays as-is — removing the `EndsWith` branch is sub-project B2, not this plan.

- [ ] **Step 5: Change the two aggregating types**

In `src/Abm.Pyro.Repository/Predicates/IResourceStorePredicateFactory.cs`, change the `StringIndex` line to:

```csharp
  Expression<Func<ResourceStore, bool>> StringIndex(SearchQueryBase searchQueryBase);
```

In `src/Abm.Pyro.Repository/Predicates/ResourceStorePredicateFactory.cs`, change the `StringIndex` method signature only — the body is unchanged:

```csharp
  public Expression<Func<ResourceStore, bool>> StringIndex(SearchQueryBase searchQueryBase)
```

- [ ] **Step 6: Collapse the String case in the search pipeline**

In `src/Abm.Pyro.Repository/Predicates/SearchSearchPredicateFactory.cs`, replace the `String` case:

```csharp
        case SearchParamType.String:
          predicateInner = predicateInner.And(resourceStorePredicateFactory.StringIndex(searchQuery));
          break;
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~StringIndexSearchTests"`
Expected: PASS, including the pre-existing string search tests.

- [ ] **Step 8: Commit**

```bash
git add src/Abm.Pyro.Repository/Predicates src/Abm.Pyro.Api.Test/Search/StringIndexSearchTests.cs
git commit -m "fix: correct ':missing' for string search by lifting negation to ResourceStore"
```

---

### Task 4: Migrate `IndexUri`

**Files:**
- Modify: `src/Abm.Pyro.Repository/Predicates/IIndexUriPredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/IndexUriPredicateFactory.cs:23-24,51`
- Modify: `src/Abm.Pyro.Repository/Predicates/IResourceStorePredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/ResourceStorePredicateFactory.cs:85-93`
- Modify: `src/Abm.Pyro.Repository/Predicates/SearchSearchPredicateFactory.cs`
- Test: `src/Abm.Pyro.Api.Test/Search/UriIndexSearchTests.cs`

**Interfaces:**
- Consumes: Task 1's composer; Task 3's established shape.
- Produces: `Expression<Func<ResourceStore, bool>> IIndexUriPredicateFactory.UriIndex(SearchQueryUri)`.

- [ ] **Step 1: Write the failing integration test**

Append to `src/Abm.Pyro.Api.Test/Search/UriIndexSearchTests.cs`, inside the existing class:

```csharp
    [Fact]
    public async Task Search_UrlMissingTrue_ReturnsOnlyValueSetWithoutUrl()
    {
        ValueSet? withUrl = await FhirClient.CreateAsync(ValueSetBuilder.Build(url: "http://example.org/vs/one"));
        ValueSet? withoutUrl = await FhirClient.CreateAsync(ValueSetBuilder.Build(includeUrl: false));
        Assert.NotNull(withUrl);
        Assert.NotNull(withoutUrl);

        Bundle? bundle = await FhirClient.SearchAsync<ValueSet>(new[] { "url:missing=true" });

        Assert.NotNull(bundle);
        Assert.Equal([withoutUrl.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    [Fact]
    public async Task Search_UrlMissingFalse_ReturnsOnlyValueSetWithUrl()
    {
        ValueSet? withUrl = await FhirClient.CreateAsync(ValueSetBuilder.Build(url: "http://example.org/vs/one"));
        ValueSet? withoutUrl = await FhirClient.CreateAsync(ValueSetBuilder.Build(includeUrl: false));
        Assert.NotNull(withUrl);
        Assert.NotNull(withoutUrl);

        Bundle? bundle = await FhirClient.SearchAsync<ValueSet>(new[] { "url:missing=false" });

        Assert.NotNull(bundle);
        Assert.Equal([withUrl.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~UriIndexSearchTests"`
Expected: the `missing=true` test FAILS with an empty bundle.

- [ ] **Step 3: Change the interface**

```csharp
using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IIndexUriPredicateFactory
{
  Expression<Func<ResourceStore, bool>> UriIndex(SearchQueryUri searchQueryUri);
}
```

- [ ] **Step 4: Rewrite the factory's structure**

In `IndexUriPredicateFactory.cs`: build a `List<IndexPredicateTerm<IndexUri>>` instead of a `List<Expression<Func<IndexUri, bool>>>`; drop the unconditional `indexUriPredicate.And(IsSearchParameterId(...))` at line 24 and instead compose each term's predicate explicitly; replace the `Missing` case at line 51 with:

```csharp
          case SearchModifierCodeId.Missing:
            terms.Add(new IndexPredicateTerm<IndexUri>(
              IsSearchParameterId(searchParameterId),
              Negated: uriValue.IsMissing));
            break;
```

Every other case adds `new IndexPredicateTerm<IndexUri>(And(IsSearchParameterId(searchParameterId), <its existing value predicate>), Negated: false)`. Delete `IsNotSearchParameterId`. Add the same private `And` helper as Task 3, typed to `IndexUri`. Return:

```csharp
    return IndexPredicateComposer.Compose(
      terms,
      PredicateCombine.Or,
      (predicate, negated) => negated
        ? x => !x.IndexUriList.Any(predicate.Compile())
        : x => x.IndexUriList.Any(predicate.Compile()));
```

- [ ] **Step 5: Change the aggregating types and the pipeline case**

`IResourceStorePredicateFactory.UriIndex` and `ResourceStorePredicateFactory.UriIndex` return `Expression<Func<ResourceStore, bool>>`; bodies unchanged. In `SearchSearchPredicateFactory`:

```csharp
        case SearchParamType.Uri:
          predicateInner = predicateInner.And(resourceStorePredicateFactory.UriIndex(searchQuery));
          break;
```

- [ ] **Step 6: Run to verify pass**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~UriIndexSearchTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Abm.Pyro.Repository/Predicates src/Abm.Pyro.Api.Test
git commit -m "fix: correct ':missing' for uri search"
```

---

### Task 5: Migrate `IndexDateTime`, and delete the spurious `ne` term

**Files:**
- Modify: `src/Abm.Pyro.Repository/Predicates/IIndexDateTimePredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/IndexDateTimePredicateFactory.cs:23-24,50-56,101`
- Modify: `src/Abm.Pyro.Repository/Predicates/IResourceStorePredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/ResourceStorePredicateFactory.cs:63-71`
- Modify: `src/Abm.Pyro.Repository/Predicates/SearchSearchPredicateFactory.cs`
- Test: `src/Abm.Pyro.Api.Test/Search/DateTimeIndexSearchTests.cs`

**Interfaces:**
- Consumes: Task 1's composer; Task 2's `PatientBuilder.Build(includeBirthDate:)`.
- Produces: `Expression<Func<ResourceStore, bool>> IIndexDateTimePredicateFactory.DateTimeIndex(SearchQueryDateTime)`.

- [ ] **Step 1: Write the failing integration tests**

Append to `src/Abm.Pyro.Api.Test/Search/DateTimeIndexSearchTests.cs`:

```csharp
    [Fact]
    public async Task Search_BirthDateMissingTrue_ReturnsOnlyPatientWithoutBirthDate()
    {
        Patient? withDate = await FhirClient.CreateAsync(PatientBuilder.Build());
        Patient? withoutDate = await FhirClient.CreateAsync(PatientBuilder.Build(includeBirthDate: false));
        Assert.NotNull(withDate);
        Assert.NotNull(withoutDate);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(new[] { "birthdate:missing=true" });

        Assert.NotNull(bundle);
        Assert.Equal([withoutDate.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    [Fact]
    public async Task Search_BirthDateNotEqual_ExcludesPatientWithoutBirthDate()
    {
        // 'ne' is a comparison: a resource with no birthdate cannot satisfy it. Absence is
        // what ':missing' is for. The bug being fixed here OR'd in 'spid <> @x', which matched
        // any resource carrying an index row for some other parameter.
        Patient? matching = await FhirClient.CreateAsync(PatientBuilder.Build());          // 1990-01-15
        Patient? withoutDate = await FhirClient.CreateAsync(PatientBuilder.Build(includeBirthDate: false));
        Assert.NotNull(matching);
        Assert.NotNull(withoutDate);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(new[] { "birthdate=ne2001-02-03" });

        Assert.NotNull(bundle);
        Assert.Equal([matching.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~DateTimeIndexSearchTests"`
Expected: the `:missing` test FAILS with an empty bundle; the `ne` test FAILS by also returning `withoutDate`.

- [ ] **Step 3: Delete the spurious `ne` term**

In `src/Abm.Pyro.Repository/Predicates/IndexDateTimePredicateFactory.cs`, remove these three lines from the `SearchComparatorId.Ne` case (lines 53–55):

```csharp
                  var searchQueryDateTimeIdPredicate = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
                  searchQueryDateTimeIdPredicate = searchQueryDateTimeIdPredicate.And(IsNotSearchParameterId(searchQueryDateTime.SearchParameter.SearchParameterStoreId.Value));
                  resultList.Add(searchQueryDateTimeIdPredicate);
```

The `NotEqualTo(...)` term immediately above them **stays** — it is the correct `ne` predicate.

- [ ] **Step 4: Change the interface and migrate the factory**

Interface returns `Expression<Func<ResourceStore, bool>> DateTimeIndex(SearchQueryDateTime searchQueryDateTime)`.

In the factory: accumulate `List<IndexPredicateTerm<IndexDateTime>>`; drop the unconditional `And(IsSearchParameterId(...))` at line 24 in favour of per-term composition via a private `And` helper typed to `IndexDateTime`; replace the `Missing` case at line 101 with:

```csharp
                  terms.Add(new IndexPredicateTerm<IndexDateTime>(
                    IsSearchParameterId(searchParameterId),
                    Negated: dateTimeValue.IsMissing));
```

Delete `IsNotSearchParameterId`. Return:

```csharp
    return IndexPredicateComposer.Compose(
      terms,
      PredicateCombine.Or,
      (predicate, negated) => negated
        ? x => !x.IndexDateTimeList.Any(predicate.Compile())
        : x => x.IndexDateTimeList.Any(predicate.Compile()));
```

- [ ] **Step 5: Change the aggregating types and the pipeline case**

```csharp
        case SearchParamType.Date:
          predicateInner = predicateInner.And(resourceStorePredicateFactory.DateTimeIndex(searchQuery));
          break;
```

- [ ] **Step 6: Run to verify pass**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~DateTimeIndexSearchTests"`
Expected: PASS, including all pre-existing date range tests.

- [ ] **Step 7: Commit**

```bash
git add src/Abm.Pyro.Repository/Predicates src/Abm.Pyro.Api.Test
git commit -m "fix: correct ':missing' for date search and drop spurious 'ne' term"
```

---

### Task 6: Migrate `IndexQuantity`

**Files:**
- Modify: `src/Abm.Pyro.Repository/Predicates/IIndexQuantityPredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/IndexQuantityPredicateFactory.cs:24-25,93`
- Modify: `src/Abm.Pyro.Repository/Predicates/IResourceStorePredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/ResourceStorePredicateFactory.cs:74-82`
- Modify: `src/Abm.Pyro.Repository/Predicates/SearchSearchPredicateFactory.cs`
- Test: `src/Abm.Pyro.Api.Test/Search/QuantityIndexSearchTests.cs`

**Interfaces:**
- Consumes: Task 1's composer; Task 2's `ObservationBuilder.Build(valueQuantityAmount:)`.
- Produces: `Expression<Func<ResourceStore, bool>> IIndexQuantityPredicateFactory.QuantityIndex(SearchQueryQuantity)`.

- [ ] **Step 1: Write the failing integration test**

Append to `src/Abm.Pyro.Api.Test/Search/QuantityIndexSearchTests.cs`:

```csharp
    [Fact]
    public async Task Search_ValueQuantityMissingTrue_ReturnsOnlyObservationWithoutValue()
    {
        Observation? withValue = await FhirClient.CreateAsync(
            ObservationBuilder.Build(valueQuantityAmount: 72.5m));
        Observation? withoutValue = await FhirClient.CreateAsync(ObservationBuilder.Build());
        Assert.NotNull(withValue);
        Assert.NotNull(withoutValue);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { "value-quantity:missing=true" });

        Assert.NotNull(bundle);
        Assert.Equal([withoutValue.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    [Fact]
    public async Task Search_ValueQuantityMissingFalse_ReturnsOnlyObservationWithValue()
    {
        Observation? withValue = await FhirClient.CreateAsync(
            ObservationBuilder.Build(valueQuantityAmount: 72.5m));
        Observation? withoutValue = await FhirClient.CreateAsync(ObservationBuilder.Build());
        Assert.NotNull(withValue);
        Assert.NotNull(withoutValue);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { "value-quantity:missing=false" });

        Assert.NotNull(bundle);
        Assert.Equal([withValue.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~QuantityIndexSearchTests"`
Expected: the `missing=true` test FAILS with an empty bundle.

- [ ] **Step 3: Change the interface and migrate the factory**

Interface returns `Expression<Func<ResourceStore, bool>> QuantityIndex(SearchQueryQuantity searchQueryQuantity)`.

In the factory: accumulate `List<IndexPredicateTerm<IndexQuantity>>`; drop the unconditional `And(IsSearchParameterId(...))` at line 25; replace the `Missing` case at line 93 with a term carrying `IsSearchParameterId(searchParameterId)` and `Negated: quantityValue.IsMissing`. Every other branch wraps its existing predicate with `And(IsSearchParameterId(searchParameterId), …)` and `Negated: false`. **`NotEqualTo` stays** — it serves the `ne` prefix, which is correct as-is. Delete `IsNotSearchParameterId`. Return:

```csharp
    return IndexPredicateComposer.Compose(
      terms,
      PredicateCombine.Or,
      (predicate, negated) => negated
        ? x => !x.IndexQuantityList.Any(predicate.Compile())
        : x => x.IndexQuantityList.Any(predicate.Compile()));
```

- [ ] **Step 4: Change the aggregating types and the pipeline case**

```csharp
        case SearchParamType.Quantity:
          predicateInner = predicateInner.And(resourceStorePredicateFactory.QuantityIndex(searchQuery));
          break;
```

- [ ] **Step 5: Run to verify pass**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~QuantityIndexSearchTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Abm.Pyro.Repository/Predicates src/Abm.Pyro.Api.Test
git commit -m "fix: correct ':missing' for quantity search"
```

---

### Task 7: Migrate `IndexNumber`

`SearchParamType.Number` also indexes into `IndexQuantity`, via a separate factory.

**Files:**
- Modify: `src/Abm.Pyro.Repository/Predicates/IIndexNumberPredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/IndexNumberPredicateFactory.cs:23-26,88-95`
- Modify: `src/Abm.Pyro.Repository/Predicates/IResourceStorePredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/ResourceStorePredicateFactory.cs:54-62`
- Modify: `src/Abm.Pyro.Repository/Predicates/SearchSearchPredicateFactory.cs`
- Test: `src/Abm.Pyro.Api.Test/Search/NumberSearchTests.cs`

**Interfaces:**
- Consumes: Task 1's composer.
- Produces: `Expression<Func<ResourceStore, bool>> IIndexNumberPredicateFactory.NumberIndex(SearchQueryNumber)`.

- [ ] **Step 1: Write the failing integration test**

Append to `src/Abm.Pyro.Api.Test/Search/NumberSearchTests.cs`, inside the existing class. `RiskAssessmentBuilder.Build(decimal? probability = null, string? subjectPatientId = null)` omits the `Prediction` element entirely when `probability` is null, so no builder change is needed here:

```csharp
    [Fact]
    public async Task Search_ProbabilityMissingTrue_ReturnsOnlyRiskAssessmentWithoutProbability()
    {
        RiskAssessment? withProbability = await FhirClient.CreateAsync(
            RiskAssessmentBuilder.Build(probability: 0.4m));
        RiskAssessment? withoutProbability = await FhirClient.CreateAsync(
            RiskAssessmentBuilder.Build());
        Assert.NotNull(withProbability);
        Assert.NotNull(withoutProbability);

        Bundle? bundle = await FhirClient.SearchAsync<RiskAssessment>(
            new[] { "probability:missing=true" });

        Assert.NotNull(bundle);
        Assert.Equal([withoutProbability.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~NumberSearchTests"`
Expected: FAIL. Today this factory's `:missing` produces `EXISTS(spid <> @x)`, so it returns whichever resources happen to carry another indexed parameter — likely both, or the wrong one.

- [ ] **Step 3: Change the interface and migrate the factory**

Interface returns `Expression<Func<ResourceStore, bool>> NumberIndex(SearchQueryNumber searchQueryNumber)`.

Note this factory applies `IsSearchParameterId` **only** inside the `!Modifier.HasValue` branch (line 26), which is why its `:missing` is "has some other parameter" rather than self-contradictory. Restructure so every term composes `IsSearchParameterId` explicitly. Replace the `Missing` case (lines 88–95, including the nested `Prefix.HasValue` check) with:

```csharp
              case SearchModifierCodeId.Missing:
                terms.Add(new IndexPredicateTerm<IndexQuantity>(
                  IsSearchParameterId(searchParameterId),
                  Negated: numberValue.IsMissing));
                break;
```

Delete the commented-out `//ResourceStorePredicate = ResourceStorePredicate.Or(AnyIndexEquals(IndexQuantityPredicate, !NumberValue.IsMissing));` line — this is the design it was reaching for. Delete `IsNotSearchParameterId`. **`NotEqualTo` stays** for the `ne` prefix. Return:

```csharp
    return IndexPredicateComposer.Compose(
      terms,
      PredicateCombine.Or,
      (predicate, negated) => negated
        ? x => !x.IndexQuantityList.Any(predicate.Compile())
        : x => x.IndexQuantityList.Any(predicate.Compile()));
```

- [ ] **Step 4: Change the aggregating types and the pipeline case**

```csharp
        case SearchParamType.Number:
          predicateInner = predicateInner.And(resourceStorePredicateFactory.NumberIndex(searchQuery));
          break;
```

- [ ] **Step 5: Run to verify pass**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~NumberSearchTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Abm.Pyro.Repository/Predicates src/Abm.Pyro.Api.Test
git commit -m "fix: correct ':missing' for number search"
```

---

### Task 8: Migrate `IndexReference`

**Files:**
- Modify: `src/Abm.Pyro.Repository/Predicates/IIndexReferencePredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/IndexReferencePredicateFactory.cs:38-52,84`
- Modify: `src/Abm.Pyro.Repository/Predicates/IResourceStorePredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/ResourceStorePredicateFactory.cs:25-33`
- Modify: `src/Abm.Pyro.Repository/Predicates/SearchSearchPredicateFactory.cs`
- Test: `src/Abm.Pyro.Api.Test/Search/ReferenceIndexSearchTests.cs`

**Interfaces:**
- Consumes: Task 1's composer; Task 2's `PatientBuilder.Build(managingOrganizationId:)`.
- Produces: `Task<Expression<Func<ResourceStore, bool>>> IIndexReferencePredicateFactory.ReferenceIndex(SearchQueryReference)` — note this one is `async`, unlike its siblings.

- [ ] **Step 1: Write the failing integration test**

Append to `src/Abm.Pyro.Api.Test/Search/ReferenceIndexSearchTests.cs`:

```csharp
    [Fact]
    public async Task Search_OrganizationMissingTrue_ReturnsOnlyPatientWithoutOrganization()
    {
        Organization? organization = await FhirClient.CreateAsync(OrganizationBuilder.Build());
        Assert.NotNull(organization);

        Patient? withOrganization = await FhirClient.CreateAsync(
            PatientBuilder.Build(managingOrganizationId: organization.Id));
        Patient? withoutOrganization = await FhirClient.CreateAsync(PatientBuilder.Build());
        Assert.NotNull(withOrganization);
        Assert.NotNull(withoutOrganization);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "organization:missing=true" });

        Assert.NotNull(bundle);
        Assert.Equal([withoutOrganization.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~ReferenceIndexSearchTests"`
Expected: FAIL with an empty bundle.

- [ ] **Step 3: Change the interface**

```csharp
using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IIndexReferencePredicateFactory
{
  Task<Expression<Func<ResourceStore, bool>>> ReferenceIndex(SearchQueryReference searchQueryReference);
}
```

- [ ] **Step 4: Migrate the factory**

This factory has two predicate builders — `quickIndexReferencePredicate` (the multi-id `IN` fast path, line 38) and `indexReferencePredicate` (line 51). Both currently seed with `IsSearchParameterId`. Accumulate `List<IndexPredicateTerm<IndexReference>>` across both paths, composing `IsSearchParameterId` into each term explicitly. Replace the `Missing` case at line 84 with:

```csharp
              case SearchModifierCodeId.Missing:
                terms.Add(new IndexPredicateTerm<IndexReference>(
                  IsSearchParameterId(searchParameterId),
                  Negated: referenceValue.IsMissing));
                break;
```

Delete `IsNotSearchParameterId`. Return:

```csharp
    return IndexPredicateComposer.Compose(
      terms,
      PredicateCombine.Or,
      (predicate, negated) => negated
        ? x => !x.IndexReferenceList.Any(predicate.Compile())
        : x => x.IndexReferenceList.Any(predicate.Compile()));
```

- [ ] **Step 5: Change the aggregating types and the pipeline case**

`IResourceStorePredicateFactory.ReferenceIndex` returns `Task<Expression<Func<ResourceStore, bool>>>`; `ResourceStorePredicateFactory.ReferenceIndex`'s body is unchanged. In `SearchSearchPredicateFactory`:

```csharp
        case SearchParamType.Reference:
          predicateInner = predicateInner.And(await resourceStorePredicateFactory.ReferenceIndex(searchQuery));
          break;
```

- [ ] **Step 6: Run to verify pass**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~ReferenceIndexSearchTests"`
Expected: PASS, including the existing multi-id `IN` fast-path tests.

- [ ] **Step 7: Commit**

```bash
git add src/Abm.Pyro.Repository/Predicates src/Abm.Pyro.Api.Test
git commit -m "fix: correct ':missing' for reference search"
```

---

### Task 9: Migrate `IndexToken`, including `:not`

The largest task: it fixes `:missing`, rewrites `:not` onto the positive predicates, and carries Review Focus items 3, 4 and 5.

**Files:**
- Modify: `src/Abm.Pyro.Repository/Predicates/IIndexTokenPredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/IndexTokenPredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/IResourceStorePredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/ResourceStorePredicateFactory.cs:44-53`
- Modify: `src/Abm.Pyro.Repository/Predicates/SearchSearchPredicateFactory.cs`
- Test: `src/Abm.Pyro.Api.Test/Search/TokenIndexSearchTests.cs`

**Interfaces:**
- Consumes: Task 1's composer; Task 2's `ObservationBuilder.Build(snomedCode:, includeCode:)` and `PatientBuilder.Build(includeGender:, includeName:)`.
- Produces: `Expression<Func<ResourceStore, bool>> IIndexTokenPredicateFactory.TokenIndex(SearchQueryToken)`.

- [ ] **Step 1: Write the failing integration tests**

Append to `src/Abm.Pyro.Api.Test/Search/TokenIndexSearchTests.cs`:

```csharp
    private const string SnomedBodyWeightCode = "27113001";

    [Fact]
    public async Task Search_CodeMissingTrue_ReturnsOnlyObservationWithoutCode()
    {
        Observation? withCode = await FhirClient.CreateAsync(ObservationBuilder.Build());
        Observation? withoutCode = await FhirClient.CreateAsync(
            ObservationBuilder.Build(includeCode: false));
        Assert.NotNull(withCode);
        Assert.NotNull(withoutCode);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(new[] { "code:missing=true" });

        Assert.NotNull(bundle);
        Assert.Equal([withoutCode.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    [Fact]
    public async Task Search_GenderNot_IncludesPatientWithNoGender()
    {
        // FHIR R4: ':not' includes resources that have no value for the parameter.
        Patient? unknownGender = await FhirClient.CreateAsync(PatientBuilder.Build());
        Patient? noGender = await FhirClient.CreateAsync(PatientBuilder.Build(includeGender: false));
        Assert.NotNull(unknownGender);
        Assert.NotNull(noGender);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(new[] { "gender:not=unknown" });

        Assert.NotNull(bundle);
        Assert.Equal([noGender.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    [Fact]
    public async Task Search_CodeNot_ExcludesObservationWithSiblingCoding()
    {
        // The false-positive case. The old predicate inverted the comparison INSIDE the EXISTS,
        // so the snomed coding satisfied 'System <> loinc' and the Observation wrongly matched
        // despite genuinely carrying loinc|29463-7.
        Observation? twoCodings = await FhirClient.CreateAsync(
            ObservationBuilder.Build(loincCode: BodyWeightCode, snomedCode: SnomedBodyWeightCode));
        Observation? otherCode = await FhirClient.CreateAsync(
            ObservationBuilder.Build(loincCode: HeartRateCode));
        Assert.NotNull(twoCodings);
        Assert.NotNull(otherCode);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code:not={CodeSystemUriSupport.Loinc}|{BodyWeightCode}" });

        Assert.NotNull(bundle);
        Assert.Equal([otherCode.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    [Fact]
    public async Task Search_CodeNotMultipleValues_ExcludesBoth()
    {
        // De Morgan: negating an OR'd value set is an AND of negations. Read as a literal OR
        // this query would match everything.
        Observation? bodyWeight = await FhirClient.CreateAsync(
            ObservationBuilder.Build(loincCode: BodyWeightCode));
        Observation? heartRate = await FhirClient.CreateAsync(
            ObservationBuilder.Build(loincCode: HeartRateCode));
        Observation? other = await FhirClient.CreateAsync(ObservationBuilder.Build(loincCode: "1234-5"));
        Assert.NotNull(bodyWeight);
        Assert.NotNull(heartRate);
        Assert.NotNull(other);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code:not={BodyWeightCode},{HeartRateCode}" });

        Assert.NotNull(bundle);
        Assert.Equal([other.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    [Fact]
    public async Task Search_CodeNotSystemOnly_ExcludesEveryObservationInThatSystem()
    {
        // Review Focus 4: the MatchSystemOnly form under negation.
        Observation? loincOnly = await FhirClient.CreateAsync(
            ObservationBuilder.Build(loincCode: BodyWeightCode));
        Observation? withoutCode = await FhirClient.CreateAsync(
            ObservationBuilder.Build(includeCode: false));
        Assert.NotNull(loincOnly);
        Assert.NotNull(withoutCode);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code:not={CodeSystemUriSupport.Loinc}|" });

        Assert.NotNull(bundle);
        Assert.Equal([withoutCode.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    [Fact]
    public async Task Search_TwoNegatedParameters_CombineWithAnd()
    {
        // Review Focus 3: negations must AND across parameters, not just within one.
        Patient? noNameNoGender = await FhirClient.CreateAsync(
            PatientBuilder.Build(includeName: false, includeGender: false));
        Patient? noNameUnknownGender = await FhirClient.CreateAsync(
            PatientBuilder.Build(includeName: false));
        Patient? namedNoGender = await FhirClient.CreateAsync(
            PatientBuilder.Build(familyName: "Smith", includeGender: false));
        Assert.NotNull(noNameNoGender);
        Assert.NotNull(noNameUnknownGender);
        Assert.NotNull(namedNoGender);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "name:missing=true", "gender:not=unknown" });

        Assert.NotNull(bundle);
        Assert.Equal([noNameNoGender.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    [Fact]
    public async Task Search_ByCodeWithEmptySystemPrefix_MatchesOnlyCodingWithNoSystem()
    {
        // Spec §5.1 guard. FHIR R4: '[parameter]=|[code]' matches where the Coding has NO system
        // property. No existing test covers this form, so without it the claim that the ':not'
        // rework removes no capability would be unguarded.
        var noSystem = ObservationBuilder.Build();
        noSystem.Code = new CodeableConcept
        {
            Coding = [new Coding { Code = BodyWeightCode }] // no System
        };

        Observation? withoutSystem = await FhirClient.CreateAsync(noSystem);
        Observation? withSystem = await FhirClient.CreateAsync(
            ObservationBuilder.Build(loincCode: BodyWeightCode));
        Assert.NotNull(withoutSystem);
        Assert.NotNull(withSystem);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code=|{BodyWeightCode}" });

        Assert.NotNull(bundle);
        Assert.Equal([withoutSystem.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    [Fact]
    public async Task Search_CodeMissingFalse_MatchesRowWithNullSystem()
    {
        // Review Focus 5: presence is presence. A coding with no system still produces an
        // IndexToken row, so ':missing=false' must match it.
        var observation = ObservationBuilder.Build();
        observation.Code = new CodeableConcept
        {
            Coding = [new Coding { Code = BodyWeightCode }] // no System
        };

        Observation? noSystem = await FhirClient.CreateAsync(observation);
        Observation? withoutCode = await FhirClient.CreateAsync(
            ObservationBuilder.Build(includeCode: false));
        Assert.NotNull(noSystem);
        Assert.NotNull(withoutCode);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(new[] { "code:missing=false" });

        Assert.NotNull(bundle);
        Assert.Equal([noSystem.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~TokenIndexSearchTests"`
Expected: `code:missing=true` returns the wrong set (the old predicate matches resources with other parameters); `gender:not=unknown` omits `noGender`; `Search_CodeNot_ExcludesObservationWithSiblingCoding` wrongly includes `twoCodings`; `Search_CodeNotMultipleValues_ExcludesBoth` returns everything. `Search_ByCodeWithEmptySystemPrefix_MatchesOnlyCodingWithNoSystem` should **PASS already** — it guards existing correct behaviour. If it fails, stop: the `|code` form is broken independently of this work and needs its own investigation.

- [ ] **Step 3: Change the interface**

```csharp
using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IIndexTokenPredicateFactory
{
  Expression<Func<ResourceStore, bool>> TokenIndex(SearchQueryToken searchQueryToken);
}
```

- [ ] **Step 4: Rewrite the factory**

Replace `src/Abm.Pyro.Repository/Predicates/IndexTokenPredicateFactory.cs` entirely:

```csharp
using System.Linq.Expressions;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Repository.Predicates;

public class IndexTokenPredicateFactory : IIndexTokenPredicateFactory
{
  public Expression<Func<ResourceStore, bool>> TokenIndex(SearchQueryToken searchQueryToken)
  {
    if (!searchQueryToken.SearchParameter.SearchParameterStoreId.HasValue)
    {
      throw new ArgumentNullException(nameof(searchQueryToken.SearchParameter.SearchParameterStoreId));
    }

    int searchParameterId = searchQueryToken.SearchParameter.SearchParameterStoreId.Value;
    var terms = new List<IndexPredicateTerm<IndexToken>>();

    // ':not' folds with And by De Morgan -- 'gender:not=male,female' must exclude both.
    // Everything else folds with Or, per FHIR's comma-separated value rule.
    PredicateCombine combine =
      searchQueryToken.Modifier == SearchModifierCodeId.Not ? PredicateCombine.And : PredicateCombine.Or;

    foreach (SearchQueryTokenValue tokenValue in searchQueryToken.ValueList)
    {
      if (!searchQueryToken.Modifier.HasValue)
      {
        terms.Add(new IndexPredicateTerm<IndexToken>(
          And(IsSearchParameterId(searchParameterId), EqualTo(tokenValue)),
          Negated: false));
        continue;
      }

      var arrayOfSupportedModifiers = FhirSearchQuerySupport.GetModifiersForSearchType(searchQueryToken.SearchParameter.Type);
      if (!arrayOfSupportedModifiers.Contains(searchQueryToken.Modifier.Value))
      {
        throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryToken.Modifier.Value.GetCode()} is not supported for search parameter types of {searchQueryToken.SearchParameter.Type.GetCode()}.");
      }

      switch (searchQueryToken.Modifier.Value)
      {
        case SearchModifierCodeId.Missing:
          terms.Add(new IndexPredicateTerm<IndexToken>(
            IsSearchParameterId(searchParameterId),
            Negated: tokenValue.IsMissing));
          break;
        case SearchModifierCodeId.Not:
          // Negation wraps the POSITIVE predicate. Inverting the comparison inside the row test
          // is satisfied by any sibling coding, which wrongly matches a resource that genuinely
          // carries the excluded value.
          terms.Add(new IndexPredicateTerm<IndexToken>(
            And(IsSearchParameterId(searchParameterId), EqualTo(tokenValue)),
            Negated: true));
          break;
        default:
          throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryToken.Modifier.Value.GetCode()} has been added to the supported list for {searchQueryToken.SearchParameter.Type.GetCode()} search parameter queries and yet no database predicate has been provided.");
      }
    }

    return IndexPredicateComposer.Compose(
      terms,
      combine,
      (predicate, negated) => negated
        ? x => !x.IndexTokenList.Any(predicate.Compile())
        : x => x.IndexTokenList.Any(predicate.Compile()));
  }

  private static Expression<Func<IndexToken, bool>> And(
    Expression<Func<IndexToken, bool>> left,
    Expression<Func<IndexToken, bool>> right)
  {
    return LinqKit.PredicateBuilder.New<IndexToken>(true).And(left).And(right);
  }

  private static Expression<Func<IndexToken, bool>> IsSearchParameterId(int searchParameterId)
  {
    return x => x.SearchParameterStoreId == searchParameterId;
  }

  private static Expression<Func<IndexToken, bool>> EqualTo(SearchQueryTokenValue tokenValue)
  {
    if (!tokenValue.SearchType.HasValue)
    {
      throw new ArgumentNullException(nameof(tokenValue.SearchType));
    }

    string code;
    string system;
    switch (tokenValue.SearchType.Value)
    {
      case SearchQueryTokenValue.TokenSearchType.MatchCodeOnly:
        code = StringSupport.ToLowerFast(tokenValue.Code!);
        return x => x.Code == code;
      case SearchQueryTokenValue.TokenSearchType.MatchSystemOnly:
        system = StringSupport.ToLowerFast(tokenValue.System!);
        return x => x.System == system;
      case SearchQueryTokenValue.TokenSearchType.MatchCodeAndSystem:
        system = StringSupport.ToLowerFast(tokenValue.System!);
        code = StringSupport.ToLowerFast(tokenValue.Code!);
        return x => x.System == system && x.Code == code;
      case SearchQueryTokenValue.TokenSearchType.MatchCodeWithNullSystem:
        code = StringSupport.ToLowerFast(tokenValue.Code!);
        return x => x.System == null && x.Code == code;
      default:
        throw new System.ComponentModel.InvalidEnumArgumentException(tokenValue.SearchType.Value.ToString(), (int)tokenValue.SearchType.Value, typeof(SearchQueryTokenValue.TokenSearchType));
    }
  }
}
```

`EqualTo` keeps all four FHIR R4 token forms unchanged. `NotEqualTo`, `IsNotSearchParameterId`, `AnyIndex` and `AnyIndexEquals` are deleted.

- [ ] **Step 5: Change the aggregating types and the pipeline case**

```csharp
        case SearchParamType.Token:
          predicateInner = predicateInner.And(resourceStorePredicateFactory.TokenIndex(searchQuery));
          break;
```

- [ ] **Step 6: Run to verify pass**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~TokenIndexSearchTests"`
Expected: PASS — the seven new tests plus every pre-existing token test, which guard that `?code=c` and `?code=|c` still behave as before.

- [ ] **Step 7: Commit**

```bash
git add src/Abm.Pyro.Repository/Predicates src/Abm.Pyro.Api.Test
git commit -m "fix: correct ':missing' and ':not' for token search"
```

---

### Task 10: Fold `PositionIndexMissing` into `PositionIndex`

**Files:**
- Modify: `src/Abm.Pyro.Repository/Predicates/IIndexPositionPredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/IndexPositionPredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/IResourceStorePredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/ResourceStorePredicateFactory.cs:95-115`
- Modify: `src/Abm.Pyro.Repository/Predicates/SearchSearchPredicateFactory.cs`
- Test: `src/Abm.Pyro.Api.Test/Search/NearSearchTests.cs`

**Interfaces:**
- Consumes: Task 1's composer.
- Produces: `Expression<Func<ResourceStore, bool>> IIndexPositionPredicateFactory.PositionIndex(SearchQueryNear)`. `PositionIndexMissing` is removed from both interfaces.

- [ ] **Step 1: Confirm the near suite is green before touching it**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~Near"`
Expected: PASS. This is the baseline — `near` behaviour must be identical afterwards.

- [ ] **Step 2: Add the tautology regression test**

Append to `src/Abm.Pyro.Api.Test/Search/NearSearchTests.cs`, inside the existing class. `LocationBuilder.Build(string? id, string? name, decimal? latitude, decimal? longitude)` omits `Position` entirely unless both coordinates are supplied, so no builder change is needed here:

```csharp
    [Fact]
    public async Task Search_NearMissingTrueAndFalse_ReturnsEveryLocation()
    {
        // ':missing=true,false' is "absent OR present" -- a tautology, and deliberately so.
        // Pinning it here because folding negated terms with And would silently make it
        // "absent AND present", which is always false.
        Location? withPosition = await FhirClient.CreateAsync(
            LocationBuilder.Build(latitude: -33.8688m, longitude: 151.2093m));
        Location? withoutPosition = await FhirClient.CreateAsync(LocationBuilder.Build());
        Assert.NotNull(withPosition);
        Assert.NotNull(withoutPosition);

        Bundle? bundle = await FhirClient.SearchAsync<Location>(new[] { "near:missing=true,false" });

        Assert.NotNull(bundle);
        Assert.Equal(
            new[] { withPosition.Id, withoutPosition.Id }.Order(),
            bundle.Entry.Select(e => e.Resource.Id).Order());
    }
```


- [ ] **Step 3: Run to confirm it passes against the current code**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~Search_NearMissingTrueAndFalse"`
Expected: PASS. This test documents existing correct behaviour; it must keep passing through the refactor.

- [ ] **Step 4: Merge the two methods**

`src/Abm.Pyro.Repository/Predicates/IIndexPositionPredicateFactory.cs`:

```csharp
using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;

namespace Abm.Pyro.Repository.Predicates;

public interface IIndexPositionPredicateFactory
{
  Expression<Func<ResourceStore, bool>> PositionIndex(SearchQueryNear searchQueryNear);
}
```

In `IndexPositionPredicateFactory.cs`, replace both public methods with one that branches on the modifier internally and returns a composed predicate:

```csharp
  public Expression<Func<ResourceStore, bool>> PositionIndex(SearchQueryNear searchQueryNear)
  {
    if (!searchQueryNear.SearchParameter.SearchParameterStoreId.HasValue)
    {
      throw new ArgumentNullException(nameof(searchQueryNear.SearchParameter.SearchParameterStoreId));
    }

    int searchParameterId = searchQueryNear.SearchParameter.SearchParameterStoreId.Value;
    var terms = new List<IndexPredicateTerm<IndexPosition>>();

    if (searchQueryNear.Modifier.HasValue)
    {
      var arrayOfSupportedModifiers = FhirSearchQuerySupport.GetModifiersForSearchType(searchQueryNear.SearchParameter.Type);
      if (!arrayOfSupportedModifiers.Contains(searchQueryNear.Modifier.Value))
      {
        throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryNear.Modifier.Value.GetCode()} is not supported for search parameter types of {searchQueryNear.SearchParameter.Type.GetCode()}.");
      }

      if (searchQueryNear.Modifier.Value != SearchModifierCodeId.Missing)
      {
        throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryNear.Modifier.Value.GetCode()} has been added to the supported list for {searchQueryNear.SearchParameter.Type.GetCode()} search parameter queries and yet no database predicate has been provided.");
      }

      foreach (SearchQueryNearValue nearValue in searchQueryNear.ValueList)
      {
        terms.Add(new IndexPredicateTerm<IndexPosition>(
          IsSearchParameterId(searchParameterId),
          Negated: nearValue.IsMissing));
      }
    }
    else
    {
      foreach (SearchQueryNearValue nearValue in searchQueryNear.ValueList)
      {
        terms.Add(new IndexPredicateTerm<IndexPosition>(
          And(IsSearchParameterId(searchParameterId), WithinDistanceOf(nearValue)),
          Negated: false));
      }
    }

    return IndexPredicateComposer.Compose(
      terms,
      PredicateCombine.Or,
      (predicate, negated) => negated
        ? x => !x.IndexPositionList.Any(predicate.Compile())
        : x => x.IndexPositionList.Any(predicate.Compile()));
  }

  private static Expression<Func<IndexPosition, bool>> And(
    Expression<Func<IndexPosition, bool>> left,
    Expression<Func<IndexPosition, bool>> right)
  {
    return LinqKit.PredicateBuilder.New<IndexPosition>(true).And(left).And(right);
  }
```

Keep `WithinDistanceOf` and `IsSearchParameterId` exactly as they are, including the coordinate-order comment.

- [ ] **Step 5: Remove `PositionIndexMissing` from the aggregating types**

Delete the `PositionIndexMissing` declaration from `IResourceStorePredicateFactory` and its implementation from `ResourceStorePredicateFactory` (lines 105–115). Change `PositionIndex` to return `Expression<Func<ResourceStore, bool>>`.

In `SearchSearchPredicateFactory`, replace the whole `Special` case — the `Missing` branch and its comment go away:

```csharp
        case SearchParamType.Special:
          predicateInner = predicateInner.And(resourceStorePredicateFactory.PositionIndex(searchQuery));
          break;
```

- [ ] **Step 6: Run the whole near suite**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~Near"`
Expected: PASS, identical to Step 1's baseline.

- [ ] **Step 7: Commit**

```bash
git add src/Abm.Pyro.Repository/Predicates src/Abm.Pyro.Api.Test/Search/NearSearchTests.cs
git commit -m "refactor: fold PositionIndexMissing into PositionIndex via the shared composer"
```

---

### Task 11: Per-factory unit tests for term shape

Spec §7 calls for unit tests pinning each factory's term shape. These run without Docker, so they
give fast feedback on the thing integration tests can only confirm indirectly: that `:missing=true`
produces a *negated* term and `:not` folds with `And`.

Because the factories now return `Expression<Func<ResourceStore, bool>>`, these tests evaluate the
returned predicate against in-memory `ResourceStore` instances rather than inspecting expression
trees. That asserts behaviour rather than structure, so a future refactor that keeps the semantics
will not break them.

**Files:**
- Test: `src/Abm.Pyro.Repository.Test/Predicates/IndexFactoryTermShapeTests.cs` (create)

**Interfaces:**
- Consumes: every factory migrated in Tasks 3–10; `IndexPredicateComposer` from Task 1.
- Produces: nothing.

- [ ] **Step 1: Write the tests**

Create `src/Abm.Pyro.Repository.Test/Predicates/IndexFactoryTermShapeTests.cs`:

```csharp
using System.Linq.Expressions;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.SearchQueryEntity;
using Abm.Pyro.Repository.Predicates;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Repository.Test.Predicates;

public class IndexFactoryTermShapeTests
{
    private const int SearchParameterId = 42;
    private const int OtherSearchParameterId = 99;

    /// <summary>
    /// Mirrors the projection-building pattern already used by
    /// Abm.Pyro.Domain.Test/SearchQueryEntity/SearchQueryNearTest.cs.
    /// </summary>
    private static SearchParameterProjection Projection(string code, SearchParamType type) =>
        new(
            searchParameterStoreId: SearchParameterId,
            code: code,
            status: PublicationStatusId.Active,
            isCurrent: true,
            isDeleted: false,
            url: new Uri($"http://hl7.org/fhir/SearchParameter/{code}"),
            type: type,
            expression: null,
            multipleOr: null,
            multipleAnd: null,
            baseList: [],
            targetList: [],
            comparatorList: [],
            modifierList: [],
            componentList: []);

    /// <summary>
    /// A ResourceStore carrying the given IndexString rows and nothing else. The factories'
    /// returned predicates are compiled and invoked against instances like this, so the tests
    /// assert behaviour rather than expression-tree shape.
    /// </summary>
    private static ResourceStore StoreWithStrings(params IndexString[] rows) =>
        new(
            resourceStoreId: 1,
            resourceId: "one",
            versionId: 1,
            resourceType: FhirResourceTypeId.Patient,
            isCurrent: true,
            isDeleted: false,
            httpVerb: HttpVerbId.Post,
            json: "{}",
            lastUpdatedUtc: DateTime.UtcNow,
            indexReferenceList: [],
            indexStringList: rows.ToList(),
            indexDateTimeList: [],
            indexQuantityList: [],
            indexTokenList: [],
            indexUriList: [],
            rowVersion: 1);

    private static ResourceStore StoreWithTokens(params IndexToken[] rows) =>
        new(
            resourceStoreId: 1,
            resourceId: "one",
            versionId: 1,
            resourceType: FhirResourceTypeId.Patient,
            isCurrent: true,
            isDeleted: false,
            httpVerb: HttpVerbId.Post,
            json: "{}",
            lastUpdatedUtc: DateTime.UtcNow,
            indexReferenceList: [],
            indexStringList: [],
            indexDateTimeList: [],
            indexQuantityList: [],
            indexTokenList: rows.ToList(),
            indexUriList: [],
            rowVersion: 1);

    /// <summary>
    /// Builds the query the parser would produce for 'name:missing=true|false', using the real
    /// ParseValue so the test exercises the same ValueList the server would build.
    /// </summary>
    private static async Task<SearchQueryString> MissingStringQuery(bool isMissing)
    {
        var query = new SearchQueryString(
            Projection("name", SearchParamType.String),
            FhirResourceTypeId.Patient,
            $"name:missing={isMissing.ToString().ToLowerInvariant()}")
        {
            Modifier = SearchModifierCodeId.Missing
        };

        await query.ParseValue(isMissing.ToString().ToLowerInvariant());
        Assert.True(query.IsValid, query.InvalidMessage);
        return query;
    }

    [Fact]
    public async Task StringIndex_MissingTrue_MatchesStoreWithNoRowForThatParameter()
    {
        var factory = new IndexStringPredicateFactory();

        Expression<Func<ResourceStore, bool>> predicate =
            factory.StringIndex(await MissingStringQuery(isMissing: true));
        Func<ResourceStore, bool> compiled = predicate.Compile();

        Assert.True(compiled(StoreWithStrings()));

        // A row for a DIFFERENT search parameter is still absence for THIS one. The old
        // 'spid <> @x' idiom got exactly this case wrong.
        Assert.True(compiled(StoreWithStrings(
            new IndexString(1, 1, null, OtherSearchParameterId, null, "smith"))));

        Assert.False(compiled(StoreWithStrings(
            new IndexString(1, 1, null, SearchParameterId, null, "smith"))));
    }

    [Fact]
    public async Task StringIndex_MissingFalse_MatchesOnlyStoreWithARowForThatParameter()
    {
        var factory = new IndexStringPredicateFactory();

        Expression<Func<ResourceStore, bool>> predicate =
            factory.StringIndex(await MissingStringQuery(isMissing: false));
        Func<ResourceStore, bool> compiled = predicate.Compile();

        Assert.False(compiled(StoreWithStrings()));
        Assert.False(compiled(StoreWithStrings(
            new IndexString(1, 1, null, OtherSearchParameterId, null, "smith"))));
        Assert.True(compiled(StoreWithStrings(
            new IndexString(1, 1, null, SearchParameterId, null, "smith"))));
    }
}
```

Note `ResourceStore`'s constructor takes `string json` (the GZip conversion happens in the EF value
converter, not the entity) and requires every index list positionally, with `indexPositionList`
optional and last.

- [ ] **Step 2: Extend to the remaining six factories**

Repeat the `MissingTrue` / `MissingFalse` pair for `IndexUriPredicateFactory.UriIndex`,
`IndexDateTimePredicateFactory.DateTimeIndex`, `IndexQuantityPredicateFactory.QuantityIndex`,
`IndexNumberPredicateFactory.NumberIndex`, `IndexReferencePredicateFactory.ReferenceIndex` (which is
`async` — `await` it) and `IndexTokenPredicateFactory.TokenIndex`, each with its own
`StoreWith<Index>` helper and its own search-query builder. The assertions are the same three cases:
no rows → matches when missing=true; a row for a different parameter → still matches when
missing=true; a row for this parameter → does not.

- [ ] **Step 3: Add the token `:not` fold test**

`IndexToken`'s constructor is `IndexToken(int? indexTokenId, int? resourceStoreId, ResourceStore? resourceStore, int? searchParameterStoreId, SearchParameterStore? searchParameterStore, string? code, string? system)` — **code before system**.

```csharp
    private static async Task<SearchQueryToken> NotTokenQuery(params string[] codes)
    {
        string rawValues = string.Join(',', codes);

        var query = new SearchQueryToken(
            Projection("gender", SearchParamType.Token),
            FhirResourceTypeId.Patient,
            $"gender:not={rawValues}")
        {
            Modifier = SearchModifierCodeId.Not
        };

        await query.ParseValue(rawValues);
        Assert.True(query.IsValid, query.InvalidMessage);
        return query;
    }

    [Fact]
    public async Task TokenIndex_NotWithTwoValues_ExcludesStoreHavingEitherValue()
    {
        // De Morgan: ':not=male,female' folds with And, so a store with EITHER value is excluded.
        // Read as a literal OR of negations this query would match everything.
        var factory = new IndexTokenPredicateFactory();

        Expression<Func<ResourceStore, bool>> predicate =
            factory.TokenIndex(await NotTokenQuery("male", "female"));
        Func<ResourceStore, bool> compiled = predicate.Compile();

        Assert.False(compiled(StoreWithTokens(
            new IndexToken(1, 1, null, SearchParameterId, null, "male", null))));
        Assert.False(compiled(StoreWithTokens(
            new IndexToken(1, 1, null, SearchParameterId, null, "female", null))));
        Assert.True(compiled(StoreWithTokens(
            new IndexToken(1, 1, null, SearchParameterId, null, "other", null))));

        // FHIR R4: ':not' includes resources with no value for the parameter.
        Assert.True(compiled(StoreWithTokens()));
    }

    [Fact]
    public async Task TokenIndex_NotWithSiblingCoding_ExcludesStoreCarryingTheValue()
    {
        // The false-positive case at the unit level: two rows for the same parameter, one of
        // which matches. Inverting inside the row test let the non-matching sibling satisfy it.
        var factory = new IndexTokenPredicateFactory();

        Expression<Func<ResourceStore, bool>> predicate =
            factory.TokenIndex(await NotTokenQuery("male"));
        Func<ResourceStore, bool> compiled = predicate.Compile();

        Assert.False(compiled(StoreWithTokens(
            new IndexToken(1, 1, null, SearchParameterId, null, "male", null),
            new IndexToken(2, 1, null, SearchParameterId, null, "other", null))));
    }
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test src/Abm.Pyro.Repository.Test/Abm.Pyro.Repository.Test.csproj`
Expected: PASS, including Task 1's six composer tests.

- [ ] **Step 5: Commit**

```bash
git add src/Abm.Pyro.Repository.Test
git commit -m "test: pin ':missing' and ':not' term shape per index factory"
```

---

### Task 12: Prove chained and `_has` `:missing` inherit the fix

No production code changes. `ChainedPredicateFactory.GetChainTargetQuery` and `HasPredicateFactory.GetReferenceIndexPredicate` both delegate their terminal node to `ISearchPredicateFactory.GetResourceStoreIndexPredicate`, so they should already be correct. Inheritance by delegation is a claim until tested.

**Files:**
- Test: `src/Abm.Pyro.Api.Test/Chaining/ChainedMissingSearchTests.cs` (create)

**Interfaces:**
- Consumes: Tasks 3–10's corrected factories; Task 2's builders (`OrganizationBuilder.Build(includeName:)`, `ObservationBuilder.Build(includeCode:)`).
- Produces: nothing.

- [ ] **Step 1: Write the tests**

Create `src/Abm.Pyro.Api.Test/Chaining/ChainedMissingSearchTests.cs`. Read an existing file in `src/Abm.Pyro.Api.Test/Chaining/` first and match its class declaration and builder usage.

```csharp
using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Chaining;

public class ChainedMissingSearchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Search_ChainedOrganizationNameMissing_ReturnsPatientWhoseOrganizationHasNoName()
    {
        Organization? named = await FhirClient.CreateAsync(OrganizationBuilder.Build(name: "Acme Health"));
        Organization? unnamed = await FhirClient.CreateAsync(OrganizationBuilder.Build(includeName: false));
        Assert.NotNull(named);
        Assert.NotNull(unnamed);

        Patient? patientOfNamed = await FhirClient.CreateAsync(
            PatientBuilder.Build(managingOrganizationId: named.Id));
        Patient? patientOfUnnamed = await FhirClient.CreateAsync(
            PatientBuilder.Build(managingOrganizationId: unnamed.Id));
        Assert.NotNull(patientOfNamed);
        Assert.NotNull(patientOfUnnamed);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "organization.name:missing=true" });

        Assert.NotNull(bundle);
        Assert.Equal([patientOfUnnamed.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    [Fact]
    public async Task Search_HasObservationCodeMissing_ReturnsPatientWithCodelessObservation()
    {
        Patient? withCodeless = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Codeless"));
        Patient? withCoded = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: "Coded"));
        Assert.NotNull(withCodeless);
        Assert.NotNull(withCoded);

        await FhirClient.CreateAsync(
            ObservationBuilder.Build(subjectPatientId: withCodeless.Id, includeCode: false));
        await FhirClient.CreateAsync(
            ObservationBuilder.Build(subjectPatientId: withCoded.Id));

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "_has:Observation:subject:code:missing=true" });

        Assert.NotNull(bundle);
        Assert.Equal([withCodeless.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }
}
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter "FullyQualifiedName~ChainedMissingSearchTests"`
Expected: PASS without any production change. If either fails, the delegation assumption is wrong for that path — stop and report before writing new production code, because that is a design finding, not a bug to patch.

- [ ] **Step 3: Commit**

```bash
git add src/Abm.Pyro.Api.Test
git commit -m "test: prove chained and _has ':missing' inherit the negation fix"
```

---

### Task 13: Verification sweep

**Files:**
- No production changes expected.

**Interfaces:**
- Consumes: everything above.
- Produces: nothing.

- [ ] **Step 1: Confirm the broken idiom is gone**

Run: `grep -rn "IsNotSearchParameterId" src --include=*.cs`
Expected: **no matches.** Any remaining occurrence is an unmigrated factory.

- [ ] **Step 2: Confirm the dead helpers and abandoned comment are gone**

Run: `grep -rn "AnyIndexEquals\|AnyIndex(" src --include=*.cs`
Expected: no matches.

- [ ] **Step 3: Confirm `PositionIndexMissing` is gone**

Run: `grep -rn "PositionIndexMissing" src --include=*.cs`
Expected: no matches.

- [ ] **Step 4: Build the CI solution filter**

Run: `dotnet build src/Abm.Pyro.CI.slnf`
Expected: succeeds with no warnings introduced by this work.

- [ ] **Step 5: Run the entire suite**

Run: `dotnet test src/Abm.Pyro.CI.slnf`
Expected: all four test projects pass — Domain.Test, Application.Test, Api.Test and the new Repository.Test. Docker must be running.

- [ ] **Step 6: Commit any final cleanup**

```bash
git add -A
git commit -m "chore: verification sweep for search negation correctness"
```

If nothing changed, skip the commit — there is nothing to record.

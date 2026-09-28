# Sub-Project B1 — Correct Negation in the FHIR Search Predicate Layer

> Date: 2026-09-28 · Status: **Approved design, ready for implementation planning** ·
> Programme context: [2026-09-27 FHIR search performance programme](2026-09-27-fhir-search-perf-harness-design.md), §3–§4.1

B1 is the first sub-project of the FHIR search performance programme, sequenced ahead of the
measurement harness because it is a live correctness defect and because it reshapes the interface the
harness will drive (programme spec §4.1).

It is a **correctness** change. It alters result sets. It changes no index and no schema.

---

## 1. Purpose

Every attempt at negation in Pyro's search predicate layer is currently expressed *inside* the
`EXISTS` subquery over an index table. That cannot work: an index-row predicate can only assert
"a row exists matching X", never "no row matches X". The consequence is that `:missing` returns
nothing at all for most search types, `:not` silently omits resources that lack the parameter, and
the `ne` prefix on dates matches almost everything.

B1 lifts negation to the `ResourceStore` level, generalising the one place that already does this
correctly — `IndexPositionPredicateFactory.PositionIndexMissing`.

## 2. Defect inventory

All line references verified against the current `development` branch.

| Defect | Location | Produced SQL | Effect |
|---|---|---|---|
| `:missing` self-contradictory | `IndexStringPredicateFactory.cs:52`, `IndexUriPredicateFactory.cs:51`, `IndexReferencePredicateFactory.cs:84`, `IndexQuantityPredicateFactory.cs:93`, `IndexDateTimePredicateFactory.cs:101` | `EXISTS(spid = @x AND spid <> @x)` | **Zero rows, always** |
| `:missing` mis-scoped | `IndexTokenPredicateFactory.cs:38`, `IndexNumberPredicateFactory.cs:91` | `EXISTS(spid <> @x)` | Matches any resource with an index row for *some other* parameter |
| `:missing` value ignored | all of the above | — | `IsMissing` is parsed onto `SearchQueryValueBase` but read **only** by `IndexPositionPredicateFactory`, so `:missing=true` and `:missing=false` are indistinguishable |
| `:not` inverts inside the `EXISTS` | `IndexTokenPredicateFactory.cs:41-48` | `EXISTS(spid = @x AND code <> @c)` | Three distinct errors: omits resources with no value for the parameter (contrary to FHIR R4); **wrongly matches** multi-valued elements where a sibling coding differs; and omits `:not=\|c` where every coding carries a system. See §5.1. |
| `ne` spurious term | `IndexDateTimePredicateFactory.cs:53-55` | adds `OR EXISTS(spid <> @x)` | **Over-matches** — `Patient?birthdate=ne1970-01-01` matches nearly every resource |

Two cases are **already correct and must not change**:

- `ne` on `IndexQuantityPredicateFactory.cs:45` and `IndexNumberPredicateFactory.cs:43` —
  `EXISTS(spid = @x AND value <> @v)`. Correct, given the decision in §3.3.
- `:missing` on `IndexPositionPredicateFactory.PositionIndexMissing` — the template for this work.

### 2.1 Modifier support, which bounds the work

From `FhirSearchQuerySupport.GetModifiersForSearchType`:

- **`:missing`** is supported for Number, Date, String, Token, Reference, Quantity, Uri and Special.
- **`:not`** is supported for **Token only**.
- **Composite** supports no modifiers at all.

So `:not` is a single-factory change, and Composite needs no negation handling.

## 3. FHIR semantics: the composition rules

FHIR R4 specifies that comma-separated values within one search parameter are **OR**'d. The rules
below derive how negation interacts with that. They are the load-bearing decisions of this design.

### 3.1 `:not` combines with AND, by De Morgan

`gender:not=male,female` admits two readings:

- Distribute the modifier across values: `¬male ∨ ¬female`. A resource with `gender=male` satisfies
  `¬female` and therefore matches; one with `gender=female` satisfies `¬male`. **Everything matches**
  and the query is meaningless.
- Negate the value set as a whole: `¬(male ∨ female)` ≡ `¬male ∧ ¬female`. Excludes both.

Only the second reading gives the query meaning, and the AND is not a choice — it follows from
De Morgan's law. This also matches HAPI's behaviour.

Per FHIR R4, `:not` **includes resources that have no value for the parameter**. Lifting the
negation to `NOT EXISTS` delivers that for free.

### 3.2 `:missing` combines with OR, because its value is a selector

`:missing=true` and `:missing=false` are not one predicate negated two ways. The boolean selects
*which assertion is being made*: `true` asserts absence, `false` asserts presence. Two distinct
assertions under one parameter combine by the ordinary comma-OR rule.

Therefore `:missing=true,false` is `absence ∨ presence` — every resource. Tautological, but
correctly derived, and consistent with the behaviour `CLAUDE.md` already documents as deliberate for
`near:missing=true,false`.

| Query | `Combine` | Terms |
|---|---|---|
| `name=smith,jones` | `Or` | both positive |
| `name:missing=true` | `Or` | one negated |
| `near:missing=true,false` | `Or` | one negated, one positive → matches everything |
| `gender:not=male,female` | `And` | both negated |

### 3.3 `ne` excludes absent values, and so stays inside the `EXISTS`

`ne` is a comparison, and a resource with no value cannot satisfy a comparison; absence is what
`:missing` exists to express. So `ne` remains `EXISTS(spid = @x AND value <> @v)`.

The consequence is narrow and welcome: `ne` needs **no** lifted negation. Quantity and Number are
already correct, and the DateTime defect is the deletion of three lines.

## 4. Design

### 4.1 Factories return `ResourceStore`-level expressions

Every consumer of a search predicate already works at `ResourceStore` level —
`IndexCompositePredicateFactory`, `ChainedPredicateFactory` and `HasPredicateFactory` all consume
`ISearchPredicateFactory.GetResourceStoreIndexPredicate`. The index-level return type
`List<Expression<Func<IndexX, bool>>>` has exactly one consumer,
`SearchSearchPredicateFactory`, which immediately wraps each element in `.Any(...)`.

That return type is therefore the *cause* of the defect: a factory knows the negation semantics but
cannot apply them, because the shape it returns cannot express negation.

`IResourceStorePredicateFactory`'s six index methods change return type to
`Expression<Func<ResourceStore, bool>>`, and `PositionIndexMissing` is removed — `PositionIndex`
absorbs it. The corresponding `IIndexXxxPredicateFactory` interfaces change likewise.

`ISearchPredicateFactory.GetResourceStoreIndexPredicate` is **unchanged**, so composite, chained and
`_has` require no interface churn.

`SearchSearchPredicateFactory`'s switch collapses to one uniform line per search type, and its
`Special` + `Missing` special case disappears.

### 4.2 The shared composer

The OR-versus-AND fold must not be duplicated across seven factories, so it lives in one internal
helper. `PredicateCombine` remains a concept, but as an argument to that helper rather than a field
on a record crossing a public interface — semantics and their application stay together, and no
layer applies an operator it does not understand.

```csharp
internal enum PredicateCombine { Or, And }

internal sealed record IndexPredicateTerm<TIndex>(
    Expression<Func<TIndex, bool>> Predicate,
    bool Negated);

internal static class IndexPredicateComposer
{
    /// <summary>
    /// Folds index-row terms into a ResourceStore-level predicate. Each term becomes
    /// navigation.Any(p) or !navigation.Any(p) according to <see cref="IndexPredicateTerm{T}.Negated"/>.
    /// Throws on an empty term list. An empty fold has no safe answer: seeded correctly it
    /// collapses to "matches nothing" for Or and "matches everything" for And, and neither is a
    /// defensible result for a search parameter that produced no terms. Failing loudly is the only
    /// honest option.
    /// </summary>
    public static Expression<Func<ResourceStore, bool>> Compose<TIndex>(
        Expression<Func<ResourceStore, IEnumerable<TIndex>>> navigation,
        IReadOnlyList<IndexPredicateTerm<TIndex>> terms,
        PredicateCombine combine);
}
```

### 4.3 The composition trap this closes

An OR-fold must start from *false* and an AND-fold from *true*. The existing code seeds
`PredicateBuilder.New<ResourceStore>(true)` and then ORs, which is correct only because LinqKit's
`ExpressionStarter` replaces its seed on first use rather than combining with it. That behaviour is
load-bearing and invisible at the call site.

`IndexPredicateComposer` throws on an empty term list rather than relying on it, so an empty set can
never silently become "matches everything".

## 5. Per-factory changes

For each of the seven factories whose `:missing` handling is broken (String, Token, Reference,
DateTime, Quantity, Number, Uri) — `IndexPositionPredicateFactory` is already correct and is covered
by the table below instead:

1. The `:missing` branch stops calling `IsNotSearchParameterId`. It emits one term per value —
   `(spid == @x, Negated: value.IsMissing)` — with `Combine = Or`.
2. `IsNotSearchParameterId` is **deleted**. It has no legitimate remaining use in any factory.
3. The factory calls `IndexPredicateComposer.Compose` with its own navigation property and returns
   the result.

Additionally:

| Factory | Extra change |
|---|---|
| `IndexTokenPredicateFactory` | `:not` emits `(spid == @x AND EqualTo(value), Negated: true)` with `Combine = And`. **`NotEqualTo` is deleted; `EqualTo` is untouched** — see §5.1. |
| `IndexDateTimePredicateFactory` | Delete lines 53–55, the spurious `ne` term. Its `NotEqualTo` **stays** — it serves the `ne` prefix (§3.3). |
| `IndexQuantityPredicateFactory`, `IndexNumberPredicateFactory` | `NotEqualTo` **stays** for `ne`. No `ne` change. |
| `IndexPositionPredicateFactory` | `PositionIndex` absorbs `PositionIndexMissing`; behaviour must be identical, including `near:missing=true,false`. |
| `IndexStringPredicateFactory`, `IndexTokenPredicateFactory` | Delete the unused private `AnyIndex` / `AnyIndexEquals` helpers. |

### 5.1 Token `:not` — `EqualTo` stays, `NotEqualTo` goes

The four positive token search forms are correct today and **are not changed by B1**. Per FHIR R4,
and as parsed by `SearchQueryToken.cs:45-90`:

| Query | Search type | Positive predicate (unchanged) |
|---|---|---|
| `?code=c` | `MatchCodeOnly` | `Code == c` — matches irrespective of the system property |
| `?code=\|c` | `MatchCodeWithNullSystem` | `System == null && Code == c` — matches only where the Coding/Identifier has no system |
| `?code=s\|c` | `MatchCodeAndSystem` | `System == s && Code == c` |
| `?code=s\|` | `MatchSystemOnly` | `System == s` |

Only the **negated** variants are removed. `:not` becomes
`NOT EXISTS(spid = @x AND EqualTo(value))`, reusing the positive predicates above.

This is a correctness gain, not a lost capability. The hand-written `NotEqualTo` inverts the
comparison *inside* the existence test, which conflates "no coding matches" with "some coding
differs" — and on multi-valued elements those are not the same thing:

| Case | Current `NotEqualTo` | Correct |
|---|---|---|
| Resource has no value for the parameter | omitted | **must match** (FHIR R4 states `:not` includes these) |
| Observation has codings `loinc\|1234` and `snomed\|9999`; query `code:not=loinc\|1234` | `EXISTS(spid AND (System <> loinc \| Code <> 1234))` — the snomed row satisfies it, so the resource **wrongly matches** | must **not** match; it does have `loinc\|1234` |
| `code:not=\|c` where every coding carries a system | requires a null-system row to exist, so the resource is omitted | **must match** — it has no system-less code `c` |

`NOT EXISTS(… EqualTo(…))` fixes all three at once, because negation sits outside the existence
test where it cannot be satisfied by a sibling row.

`IndexNumberPredicateFactory.cs:92` carries a commented-out
`AnyIndexEquals(IndexQuantityPredicate, !NumberValue.IsMissing)` — the original author's correct
intent, abandoned. Delete the comment; the design now implements it.

## 6. Chained and `_has`

**No production code changes.** `ChainedPredicateFactory.GetChainTargetQuery` and
`HasPredicateFactory.GetReferenceIndexPredicate` both delegate their terminal node to
`GetResourceStoreIndexPredicate`, so they inherit the fix. Negating a chain *link* is not expressible
in FHIR — "no general-practitioner at all" is `general-practitioner:missing=true`, a direct
reference `:missing`.

Inheritance by delegation is a claim until proven, so both forms are covered by integration tests
(§7).

## 7. Test strategy

TDD throughout. Ordering matters, because most of these tests **fail today by returning nothing**,
which is easy to mistake for a passing negative assertion. Every integration test therefore asserts
**exact membership**, never only a count: a bug that returns the right number of wrong rows is
precisely what has already happened here.

**Unit — term shape**, per factory:
- `:missing=true` emits one negated term, `Combine = Or`.
- `:missing=false` emits one positive term, `Combine = Or`.
- Token `:not` emits negated terms, `Combine = And`, wrapping the *positive* value predicate.

**Unit — composition**, against `IndexPredicateComposer`:
- The four rows of §3.2's table.
- `near:missing=true,false` as an explicit regression test — this case disproved an earlier draft of
  this design and must stay pinned.
- Empty term list throws.

**Integration — direct search**, for each of the seven types: seed three resources (has the value,
has a different value, parameter absent) and assert exact membership for `:missing=true` and
`:missing=false`; for Token additionally `:not`, which must include the absent-parameter resource.

**Integration — token `:not`, the three cases of §5.1**, each a distinct regression test:
- A resource with no value for the parameter **must** match `code:not=c`.
- An Observation carrying both `loinc|1234` and `snomed|9999` must **not** match
  `code:not=loinc|1234`. This is the false-positive case and the one most likely to regress, because
  it requires a multi-coding fixture rather than the single-value fixtures used elsewhere.
- A resource whose codings all carry a system **must** match `code:not=|c`.

**Integration — token positive forms unchanged**: `?code=c` matches irrespective of system, and
`?code=|c` matches only codings with no system. These pass today and must continue to; they guard
the claim that §5.1 removes no capability.

**Integration — `ne`**: `birthdate=ne<date>` must exclude the absent-value resource and must not
match resources that merely have other indexed parameters.

**Integration — chained and `_has`**: `Patient?general-practitioner.name:missing=true` and the `_has`
equivalent.

**Regression — `near`**: the existing Location `near` tests must pass unchanged, including
`near:missing=true`, `near:missing=false` and `near:missing=true,false`.

## 8. Out of scope

- Index shape and column design — sub-project D.
- Removing the string `EndsWith` branch — sub-project B2.
- The unconstrained `ResourceType` on the final `_has` node, visible as a commented-out line at
  `HasPredicateFactory.cs:77`. Pre-existing, unrelated to negation.
- Token modifiers listed as unimplemented in `FhirSearchQuerySupport` (`:text`, `:in`, `:not-in`,
  `:above`, `:below`).
- `ne` semantics for absent values are **settled** (§3.3), not open.

## 9. Success criteria

1. `:missing=true` returns exactly the resources with no index row for the parameter, for all seven
   search types; `:missing=false` returns exactly those with at least one.
2. `gender:not=male` returns resources whose gender is not male **and** resources with no gender.
3. `gender:not=male,female` excludes both, rather than matching everything.
3a. An Observation carrying both `loinc|1234` and `snomed|9999` is **not** returned by
   `code:not=loinc|1234` (§5.1's false-positive case).
3b. The four positive token forms are unchanged: `?code=c` still matches irrespective of system, and
   `?code=|c` still matches only system-less codings.
4. `birthdate=ne<date>` excludes resources with no birthdate and does not match on unrelated
   parameters.
5. `near` behaviour is bit-for-bit unchanged, including `near:missing=true,false`.
6. Chained and `_has` `:missing` forms return correct result sets.
7. `IsNotSearchParameterId` no longer exists anywhere in the codebase.
8. All existing tests pass; the full suite is green.

## 10. Risks

| Risk | Mitigation |
|---|---|
| The interface change touches seven factories plus the search pipeline in one diff | `GetResourceStoreIndexPredicate`'s contract is unchanged, so the blast radius stops at `SearchSearchPredicateFactory`. Migrate one factory at a time, suite green between each. |
| Result sets change, so a downstream consumer may depend on today's behaviour | `:missing` currently returns zero rows for five types; nothing can usefully depend on that. `ne` on dates currently over-matches; nothing should depend on that either. Both are recorded as deliberate behaviour changes. |
| `near` regression while absorbing `PositionIndexMissing` | Pinned by the existing `near` test suite plus the explicit `true,false` case. |
| Multi-value `:not` semantics could be contested | §3.1 records the derivation and the HAPI precedent; the decision is explicit rather than incidental. |

## 11. Open items

None. The two semantic questions that arose during design — `ne` and absent values (§3.3), and
multi-value `:not` composition (§3.1) — are both settled and recorded above.

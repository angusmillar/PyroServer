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

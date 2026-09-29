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

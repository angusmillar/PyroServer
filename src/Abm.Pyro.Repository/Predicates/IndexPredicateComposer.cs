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

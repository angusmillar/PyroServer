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

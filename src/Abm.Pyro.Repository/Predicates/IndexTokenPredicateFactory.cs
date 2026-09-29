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
          AndAlso(IsSearchParameterId(searchParameterId), EqualTo(tokenValue)),
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
          // ':missing=true' asserts absence, ':missing=false' asserts presence — two distinct
          // assertions selected by the boolean, folded with Or.
          terms.Add(new IndexPredicateTerm<IndexToken>(
            IsSearchParameterId(searchParameterId),
            Negated: tokenValue.IsMissing));
          break;
        case SearchModifierCodeId.Not:
          // Negation wraps the POSITIVE predicate. Inverting the comparison inside the row test
          // is satisfied by any sibling coding, which wrongly matches a resource that genuinely
          // carries the excluded value. Wrapping the positive form also means a resource with no
          // value for the parameter matches, as FHIR R4 requires.
          terms.Add(new IndexPredicateTerm<IndexToken>(
            AndAlso(IsSearchParameterId(searchParameterId), EqualTo(tokenValue)),
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

  /// <summary>
  /// Conjoins two index-row predicates. Named AndAlso rather than And because a private static
  /// 'And' shadows LinqKit's And extension method inside this class. The extension is invoked
  /// statically so no 'using LinqKit' is needed and no name resolution is ambiguous.
  /// </summary>
  private static Expression<Func<IndexToken, bool>> AndAlso(
    Expression<Func<IndexToken, bool>> left,
    Expression<Func<IndexToken, bool>> right)
  {
    return LinqKit.PredicateBuilder.And(left, right);
  }

  private static Expression<Func<IndexToken, bool>> IsSearchParameterId(int searchParameterId)
  {
    return x => x.SearchParameterStoreId == searchParameterId;
  }

  /// <summary>
  /// The four FHIR R4 token match forms. Unchanged by the negation rework: ':not' now wraps these
  /// positive predicates in NOT EXISTS rather than using hand-inverted comparisons.
  /// </summary>
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

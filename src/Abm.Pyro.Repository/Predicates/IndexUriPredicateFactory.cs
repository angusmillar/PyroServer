using System.Linq.Expressions;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;

namespace Abm.Pyro.Repository.Predicates;

public class IndexUriPredicateFactory : IIndexUriPredicateFactory
{
  public Expression<Func<ResourceStore, bool>> UriIndex(SearchQueryUri searchQueryUri)
  {
    if (!searchQueryUri.SearchParameter.SearchParameterStoreId.HasValue)
    {
      throw new ArgumentNullException(nameof(searchQueryUri.SearchParameter.SearchParameterStoreId));
    }

    int searchParameterId = searchQueryUri.SearchParameter.SearchParameterStoreId.Value;
    var terms = new List<IndexPredicateTerm<IndexUri>>();

    foreach (SearchQueryUriValue uriValue in searchQueryUri.ValueList)
    {
      if (!searchQueryUri.Modifier.HasValue)
      {
        if (uriValue.Value is null)
        {
          throw new ArgumentNullException(nameof(uriValue.Value));
        }

        terms.Add(new IndexPredicateTerm<IndexUri>(
          AndAlso(IsSearchParameterId(searchParameterId), EqualTo(uriValue.Value.OriginalString)),
          Negated: false));
        continue;
      }

      var arrayOfSupportedModifiers = FhirSearchQuerySupport.GetModifiersForSearchType(searchQueryUri.SearchParameter.Type);
      if (!arrayOfSupportedModifiers.Contains(searchQueryUri.Modifier.Value))
      {
        throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryUri.Modifier.Value.GetCode()} is not supported for search parameter types of {searchQueryUri.SearchParameter.Type.GetCode()}.");
      }

      if (searchQueryUri.Modifier.Value != SearchModifierCodeId.Missing && uriValue.Value is null)
      {
        throw new ArgumentNullException(nameof(uriValue.Value));
      }

      switch (searchQueryUri.Modifier.Value)
      {
        case SearchModifierCodeId.Missing:
          // ':missing=true' asserts absence, ':missing=false' asserts presence — two distinct
          // assertions selected by the boolean, folded with Or like any other value list.
          terms.Add(new IndexPredicateTerm<IndexUri>(
            IsSearchParameterId(searchParameterId),
            Negated: uriValue.IsMissing));
          break;
        case SearchModifierCodeId.Exact:
          terms.Add(new IndexPredicateTerm<IndexUri>(
            AndAlso(IsSearchParameterId(searchParameterId), EqualTo(uriValue.Value!.OriginalString)),
            Negated: false));
          break;
        case SearchModifierCodeId.Contains:
          terms.Add(new IndexPredicateTerm<IndexUri>(
            AndAlso(IsSearchParameterId(searchParameterId), Contains(uriValue.Value!.OriginalString)),
            Negated: false));
          break;
        case SearchModifierCodeId.Below:
          terms.Add(new IndexPredicateTerm<IndexUri>(
            AndAlso(IsSearchParameterId(searchParameterId), StartsWith(uriValue.Value!.OriginalString)),
            Negated: false));
          break;
        case SearchModifierCodeId.Above:
          terms.Add(new IndexPredicateTerm<IndexUri>(
            AndAlso(IsSearchParameterId(searchParameterId), EndsWith(uriValue.Value!.OriginalString)),
            Negated: false));
          break;
        default:
          throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryUri.Modifier.Value.GetCode()} has been added to the supported list for {searchQueryUri.SearchParameter.Type.GetCode()} search parameter queries and yet no database predicate has been provided.");
      }
    }

    return IndexPredicateComposer.Compose(
      terms,
      PredicateCombine.Or,
      (predicate, negated) => negated
        ? x => !x.IndexUriList.Any(predicate.Compile())
        : x => x.IndexUriList.Any(predicate.Compile()));
  }

  private static Expression<Func<IndexUri, bool>> AndAlso(
    Expression<Func<IndexUri, bool>> left,
    Expression<Func<IndexUri, bool>> right)
  {
    return LinqKit.PredicateBuilder.And(left, right);
  }

  private static Expression<Func<IndexUri, bool>> IsSearchParameterId(int searchParameterId)
  {
    return x => x.SearchParameterStoreId == searchParameterId;
  }

  private static Expression<Func<IndexUri, bool>> StartsWith(string value)
  {
    return x => x.Uri.StartsWith(value);
  }

  private static Expression<Func<IndexUri, bool>> EndsWith(string value)
  {
    return x => x.Uri.EndsWith(value);
  }

  private static Expression<Func<IndexUri, bool>> EqualTo(string value)
  {
    return x => x.Uri.Equals(value);
  }

  private static Expression<Func<IndexUri, bool>> Contains(string value)
  {
    return x => x.Uri.Contains(value);
  }
}

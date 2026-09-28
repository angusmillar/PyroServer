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

    foreach (SearchQueryStringValue stringValue in searchQueryString.ValueList)
    {
      if (!searchQueryString.Modifier.HasValue)
      {
        if (stringValue.Value is null)
        {
          throw new ArgumentNullException(nameof(stringValue.Value));
        }

        terms.Add(new IndexPredicateTerm<IndexString>(
          AndAlso(IsSearchParameterId(searchParameterId), StartsWithOrEndsWith(stringValue.Value)),
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
            AndAlso(IsSearchParameterId(searchParameterId), EqualTo(stringValue.Value!)),
            Negated: false));
          break;
        case SearchModifierCodeId.Contains:
          terms.Add(new IndexPredicateTerm<IndexString>(
            AndAlso(IsSearchParameterId(searchParameterId), Contains(stringValue.Value!)),
            Negated: false));
          break;
        default:
          throw new FhirFatalException(HttpStatusCode.InternalServerError, $"The search query modifier: {searchQueryString.Modifier.Value.GetCode()} has been added to the supported list for {searchQueryString.SearchParameter.Type.GetCode()} search parameter queries and yet no database predicate has been provided. ");
      }
    }

    return IndexPredicateComposer.Compose(
      terms,
      PredicateCombine.Or,
      (predicate, negated) => negated
        ? x => !x.IndexStringList.Any(predicate.Compile())
        : x => x.IndexStringList.Any(predicate.Compile()));
  }

  /// <summary>
  /// Conjoins two index-row predicates. Named AndAlso rather than And because a private static
  /// 'And' shadows LinqKit's And extension method inside this class. The extension is invoked
  /// statically so no 'using LinqKit' is needed and no name resolution is ambiguous.
  /// </summary>
  private static Expression<Func<IndexString, bool>> AndAlso(
    Expression<Func<IndexString, bool>> left,
    Expression<Func<IndexString, bool>> right)
  {
    return LinqKit.PredicateBuilder.And(left, right);
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

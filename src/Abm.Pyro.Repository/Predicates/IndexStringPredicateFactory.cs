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
  public List<Expression<Func<IndexString, bool>>> StringIndex(SearchQueryString searchQueryString)
  {
    var resultList = new List<Expression<Func<IndexString, bool>>>();

    foreach (SearchQueryStringValue stringValue in searchQueryString.ValueList)
    {
      if (!searchQueryString.SearchParameter.SearchParameterStoreId.HasValue)
      {
        throw new ArgumentNullException(nameof(searchQueryString.SearchParameter.SearchParameterStoreId));
      }

      var indexStringPredicate = LinqKit.PredicateBuilder.New<IndexString>();
      indexStringPredicate = indexStringPredicate.And(IsSearchParameterId(searchQueryString.SearchParameter.SearchParameterStoreId.Value));

      if (!searchQueryString.Modifier.HasValue)
      {
        if (stringValue.Value is null)
        {
          throw new ArgumentNullException(nameof(stringValue.Value));
        }
        indexStringPredicate = indexStringPredicate.And(StartsWithOrEndsWith(stringValue.Value));
        resultList.Add(indexStringPredicate);
      }
      else
      {
        var arrayOfSupportedModifiers = FhirSearchQuerySupport.GetModifiersForSearchType(searchQueryString.SearchParameter.Type);
        if (arrayOfSupportedModifiers.Contains(searchQueryString.Modifier.Value))
        {
          if (searchQueryString.Modifier.Value != SearchModifierCodeId.Missing)
          {
            if (stringValue.Value is null)
            {
              throw new ArgumentNullException(nameof(stringValue.Value));
            }
          }

          switch (searchQueryString.Modifier.Value)
          {
            case SearchModifierCodeId.Missing:
              indexStringPredicate = indexStringPredicate.And(IsNotSearchParameterId(searchQueryString.SearchParameter.SearchParameterStoreId.Value));
              resultList.Add(indexStringPredicate);
              break;
            case SearchModifierCodeId.Exact:
              indexStringPredicate = indexStringPredicate.And(EqualTo(stringValue.Value!));
              resultList.Add(indexStringPredicate);
              break;
            case SearchModifierCodeId.Contains:
              indexStringPredicate = indexStringPredicate.And(Contains(stringValue.Value!));
              resultList.Add(indexStringPredicate);
              break;
            default:
              throw new FhirFatalException(HttpStatusCode.InternalServerError, $"The search query modifier: {searchQueryString.Modifier.Value.GetCode()} has been added to the supported list for {searchQueryString.SearchParameter.Type.GetCode()} search parameter queries and yet no database predicate has been provided. ");
          }
        }
        else
        {
          throw new FhirFatalException(HttpStatusCode.InternalServerError, $"Internal Server Error: The search query modifier: {searchQueryString.Modifier.Value.GetCode()} is not supported for search parameter types of {searchQueryString.SearchParameter.Type.GetCode()}. ");
        }
      }

    }
    return resultList;
  }

  private Expression<Func<ResourceStore, bool>> AnyIndex(Expression<Func<IndexString, bool>> predicate)
  {
    return x => x.IndexStringList.Any(predicate.Compile());
  }
  private Expression<Func<ResourceStore, bool>> AnyIndexEquals(Expression<Func<IndexString, bool>> predicate, bool equals)
  {
    return x => x.IndexStringList.Any(predicate.Compile()) == equals;
  }
  private Expression<Func<IndexString, bool>> IsSearchParameterId(int searchParameterId)
  {
    return x => x.SearchParameterStoreId == searchParameterId;
  }
  private Expression<Func<IndexString, bool>> IsNotSearchParameterId(int searchParameterId)
  {
    return x => x.SearchParameterStoreId != searchParameterId;
  }
  private Expression<Func<IndexString, bool>> StartsWithOrEndsWith(string stringValue)
  {
    return x => (x.Value.StartsWith(stringValue) || x.Value.EndsWith(stringValue));
  }
  private Expression<Func<IndexString, bool>> EqualTo(string stringValue)
  {
    return x => x.Value.Equals(stringValue);
  }
  private Expression<Func<IndexString, bool>> Contains(string stringValue)
  {
    return x => x.Value.Contains(stringValue);
  }

}

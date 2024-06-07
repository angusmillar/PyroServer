using System.Linq.Expressions;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;

namespace Abm.Pyro.Repository.Predicates;

public class IndexUriPredicateFactory : IIndexUriPredicateFactory
{
  public List<Expression<Func<IndexUri, bool>>> UriIndex(SearchQueryUri searchQueryUri)
  {
    var resultList = new List<Expression<Func<IndexUri, bool>>>();
    //var ResourceStorePredicate = LinqKit.PredicateBuilder.New<ResourceStore>(true);

    foreach (SearchQueryUriValue uriValue in searchQueryUri.ValueList)
    {
      if (!searchQueryUri.SearchParameter.SearchParameterStoreId.HasValue)
      {
        throw new ArgumentNullException(nameof(searchQueryUri.SearchParameter.SearchParameterStoreId));
      }

      var indexUriPredicate = LinqKit.PredicateBuilder.New<IndexUri>(true);
      indexUriPredicate = indexUriPredicate.And(IsSearchParameterId(searchQueryUri.SearchParameter.SearchParameterStoreId.Value));

      if (!searchQueryUri.Modifier.HasValue)
      {
        if (uriValue.Value is null)
        {
          throw new ArgumentNullException(nameof(uriValue.Value));
        }

        indexUriPredicate = indexUriPredicate.And(EqualTo(uriValue.Value.OriginalString));
        resultList.Add(indexUriPredicate);
      }
      else
      {
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
            indexUriPredicate = indexUriPredicate.And(IsNotSearchParameterId(searchQueryUri.SearchParameter.SearchParameterStoreId.Value));
            resultList.Add(indexUriPredicate);
            break;
          case SearchModifierCodeId.Exact:
            indexUriPredicate = indexUriPredicate.And(EqualTo(uriValue.Value!.OriginalString));
            resultList.Add(indexUriPredicate);
            break;
          case SearchModifierCodeId.Contains:
            indexUriPredicate = indexUriPredicate.And(Contains(uriValue.Value!.OriginalString));
            resultList.Add(indexUriPredicate);
            break;
          case SearchModifierCodeId.Below:
            indexUriPredicate = indexUriPredicate.And(StartsWith(uriValue.Value!.OriginalString));
            resultList.Add(indexUriPredicate);
            break;
          case SearchModifierCodeId.Above:
            indexUriPredicate = indexUriPredicate.And(EndsWith(uriValue.Value!.OriginalString));
            resultList.Add(indexUriPredicate);
            break;
          default:
            throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryUri.Modifier.Value.GetCode()} has been added to the supported list for {searchQueryUri.SearchParameter.Type.GetCode()} search parameter queries and yet no database predicate has been provided.");
        }
      }
    }
    return resultList;
  }

  private Expression<Func<ResourceStore, bool>> AnyIndex(Expression<Func<IndexUri, bool>> predicate)
  {
    return x => x.IndexUriList.Any(predicate.Compile());
  }
  private Expression<Func<ResourceStore, bool>> AnyIndexEquals(Expression<Func<IndexUri, bool>> predicate, bool equals)
  {
    return x => x.IndexUriList.Any(predicate.Compile()) == equals;
  }
  private Expression<Func<IndexUri, bool>> IsSearchParameterId(int searchParameterId)
  {
    return x => x.SearchParameterStoreId == searchParameterId;
  }
  private Expression<Func<IndexUri, bool>> IsNotSearchParameterId(int searchParameterId)
  {
    return x => x.SearchParameterStoreId != searchParameterId;
  }
  private Expression<Func<IndexUri, bool>> StartsWith(string value)
  {
    return x => x.Uri.StartsWith(value);
  }
  private Expression<Func<IndexUri, bool>> EndsWith(string value)
  {
    return x => x.Uri.EndsWith(value);
  }
  private Expression<Func<IndexUri, bool>> EqualTo(string value)
  {
    return x => x.Uri.Equals(value);
  }
  private Expression<Func<IndexUri, bool>> Contains(string value)
  {
    return x => x.Uri.Contains(value);
  }

}

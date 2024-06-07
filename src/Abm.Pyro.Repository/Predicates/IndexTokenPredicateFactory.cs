using System.Linq.Expressions;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Repository.Predicates;

  public class IndexTokenPredicateFactory : IIndexTokenPredicateFactory
  {
    public List<Expression<Func<IndexToken, bool>>> TokenIndex(SearchQueryToken searchQueryToken)
    {
      var resultList = new List<Expression<Func<IndexToken, bool>>>();
      
      foreach (SearchQueryTokenValue tokenValue in searchQueryToken.ValueList)
      {
        if (!searchQueryToken.SearchParameter.SearchParameterStoreId.HasValue)
        {
          throw new ArgumentNullException(nameof(searchQueryToken.SearchParameter.SearchParameterStoreId));
        }
        
        var indexTokenPredicate = LinqKit.PredicateBuilder.New<IndexToken>(true);
        indexTokenPredicate = indexTokenPredicate.And(IsSearchParameterId(searchQueryToken.SearchParameter.SearchParameterStoreId.Value));

        if (!searchQueryToken.Modifier.HasValue)
        {
          indexTokenPredicate = indexTokenPredicate.And(EqualTo(tokenValue));
          resultList.Add(indexTokenPredicate);
        }
        else
        {
          var arrayOfSupportedModifiers = FhirSearchQuerySupport.GetModifiersForSearchType(searchQueryToken.SearchParameter.Type);
          if (arrayOfSupportedModifiers.Contains(searchQueryToken.Modifier.Value))
          {
            switch (searchQueryToken.Modifier.Value)
            {
              case SearchModifierCodeId.Missing:
                indexTokenPredicate = indexTokenPredicate.And(IsNotSearchParameterId(searchQueryToken.SearchParameter.SearchParameterStoreId.Value));
                resultList.Add(indexTokenPredicate);
                break;
              default:
                throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryToken.Modifier.Value.GetCode()} has been added to the supported list for {searchQueryToken.SearchParameter.Type.GetCode()} search parameter queries and yet no database predicate has been provided.");
            }
          }
          else
          {
            throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryToken.Modifier.Value.GetCode()} is not supported for search parameter types of {searchQueryToken.SearchParameter.Type.GetCode()}.");
          }
        }
      }
      return resultList;
    }

    private Expression<Func<ResourceStore, bool>> AnyIndex(Expression<Func<IndexToken, bool>> predicate)
    {
      return x => x.IndexTokenList.Any(predicate.Compile());
    }
    private Expression<Func<ResourceStore, bool>> AnyIndexEquals(Expression<Func<IndexToken, bool>> predicate, bool equals)
    {
      return x => x.IndexTokenList.Any(predicate.Compile()) == equals;
    }
    private Expression<Func<IndexToken, bool>> IsSearchParameterId(int searchParameterId)
    {
      return x => x.SearchParameterStoreId == searchParameterId;
    }
    private Expression<Func<IndexToken, bool>> IsNotSearchParameterId(int searchParameterId)
    {
      return x => x.SearchParameterStoreId != searchParameterId;
    }
    private Expression<Func<IndexToken, bool>> EqualTo(SearchQueryTokenValue tokenValue)
    {
      if (tokenValue.SearchType.HasValue)
      {
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
      throw new ArgumentNullException(nameof(tokenValue.SearchType));
    }
  }


using System.Linq.Expressions;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Repository.Predicates;

  public class IndexNumberPredicateFactory : IIndexNumberPredicateFactory
  {
    public List<Expression<Func<IndexQuantity, bool>>> NumberIndex(SearchQueryNumber searchQueryNumber)
    {
      var resultList = new List<Expression<Func<IndexQuantity, bool>>>();

      foreach (SearchQueryNumberValue numberValue in searchQueryNumber.ValueList)
      {
        if (!searchQueryNumber.SearchParameter.SearchParameterStoreId.HasValue)
        {
          throw new ArgumentNullException(nameof(searchQueryNumber.SearchParameter.SearchParameterStoreId));
        }
        
        var indexQuantityPredicate = LinqKit.PredicateBuilder.New<IndexQuantity>(true);        
        if (!searchQueryNumber.Modifier.HasValue)
        {
          indexQuantityPredicate = indexQuantityPredicate.And(IsSearchParameterId(searchQueryNumber.SearchParameter.SearchParameterStoreId.Value));
          if (!numberValue.Prefix.HasValue)
          {
            indexQuantityPredicate = indexQuantityPredicate.And(EqualTo(numberValue));
            resultList.Add(indexQuantityPredicate);
          }
          else
          {
            var arrayOfSupportedPrefixes = FhirSearchQuerySupport.GetPrefixListForSearchType(searchQueryNumber.SearchParameter.Type);
            if (arrayOfSupportedPrefixes.Contains(numberValue.Prefix.Value))
            {
              switch (numberValue.Prefix.Value)
              {
                case SearchComparatorId.Eq:
                  indexQuantityPredicate = indexQuantityPredicate.And(EqualTo(numberValue));
                  resultList.Add(indexQuantityPredicate);
                  break;
                case SearchComparatorId.Ne:
                  indexQuantityPredicate = indexQuantityPredicate.And(NotEqualTo(numberValue));
                  resultList.Add(indexQuantityPredicate);
                  break;
                case SearchComparatorId.Gt:
                  indexQuantityPredicate = indexQuantityPredicate.And(GreaterThan(numberValue));
                  resultList.Add(indexQuantityPredicate);
                  break;
                case SearchComparatorId.Lt:
                  indexQuantityPredicate = indexQuantityPredicate.And(LessThan(numberValue));
                  resultList.Add(indexQuantityPredicate);
                  break;
                case SearchComparatorId.Ge:
                  indexQuantityPredicate = indexQuantityPredicate.And(GreaterThanOrEqualTo(numberValue));
                  resultList.Add(indexQuantityPredicate);
                  break;
                case SearchComparatorId.Le:
                  indexQuantityPredicate = indexQuantityPredicate.And(LessThanOrEqualTo(numberValue));
                  resultList.Add(indexQuantityPredicate);
                  break;
                default:
                  throw new System.ComponentModel.InvalidEnumArgumentException(numberValue.Prefix.Value.GetCode(), (int)numberValue.Prefix.Value, typeof(SearchComparatorId));
              }
            }
            else
            {
              string supportedPrefixes = String.Join(',', arrayOfSupportedPrefixes);
              throw new ApplicationException($"Internal Server Error: The search query prefix: {numberValue.Prefix.Value.GetCode()} is not supported for search parameter types of: {searchQueryNumber.SearchParameter.Type.GetCode()}. The supported prefixes are: {supportedPrefixes}");
            }
          }
        }
        else
        {
          var arrayOfSupportedModifiers = FhirSearchQuerySupport.GetModifiersForSearchType(searchQueryNumber.SearchParameter.Type);
          if (arrayOfSupportedModifiers.Contains(searchQueryNumber.Modifier.Value))
          {
            if (searchQueryNumber.Modifier.Value != SearchModifierCodeId.Missing)
            {
              if (numberValue.Value is null)
              {
                throw new ArgumentNullException(nameof(numberValue.Value));
              }
            }
            switch (searchQueryNumber.Modifier.Value)
            {
              case SearchModifierCodeId.Missing:
                if (numberValue.Prefix.HasValue == false)
                {                  
                  indexQuantityPredicate = indexQuantityPredicate.And(IsNotSearchParameterId(searchQueryNumber.SearchParameter.SearchParameterStoreId.Value));
                  //ResourceStorePredicate = ResourceStorePredicate.Or(AnyIndexEquals(IndexQuantityPredicate, !NumberValue.IsMissing));
                  resultList.Add(indexQuantityPredicate);
                }
                else
                {
                  throw new ApplicationException($"Internal Server Error: Encountered a Number Query with a: {SearchModifierCodeId.Missing.GetCode()} modifier and a prefix of {numberValue.Prefix!.GetCode()}. This should not happen as a missing parameter value must be only True or False with no prefix.");
                }
                break;
              default:
                throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryNumber.Modifier.Value.GetCode()} has been added to the supported list for {searchQueryNumber.SearchParameter.Type.GetCode()} search parameter queries and yet no database predicate has been provided.");
            }
          }
          else
          {
            throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryNumber.Modifier.Value.GetCode()} is not supported for search parameter types of {searchQueryNumber.SearchParameter.Type.GetCode()}.");
          }
        }
      }
      return resultList;
    }

    private Expression<Func<IndexQuantity, bool>> EqualTo(SearchQueryNumberValue numberValue)
    {
      var predicate = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      if (numberValue.Value.HasValue && numberValue.Scale.HasValue)
      {
        predicate = predicate.And(NumberEqualTo(numberValue.Value.Value, numberValue.Scale.Value));
        return predicate;
      }
      else
      {
        throw new ArgumentNullException($"Internal Server Error: The {nameof(numberValue)} property of {nameof(numberValue.Value)} was found to be null.");
      }
    }

    private Expression<Func<IndexQuantity, bool>> NotEqualTo(SearchQueryNumberValue numberValue)
    {
      var predicate = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      if (numberValue.Value.HasValue && numberValue.Scale.HasValue)
      {
        predicate = predicate.And(NumberNotEqualTo(numberValue.Value.Value, numberValue.Scale.Value));
        return predicate;
      }
      else
      {
        throw new ArgumentNullException($"Internal Server Error: The {nameof(numberValue)} property of {nameof(numberValue.Value)} was found to be null.");
      }
    }

    private Expression<Func<IndexQuantity, bool>> GreaterThan(SearchQueryNumberValue numberValue)
    {
      var predicate = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      if (numberValue.Value.HasValue && numberValue.Scale.HasValue)
      {
        predicate = predicate.And(NumberGreaterThen(numberValue.Value.Value));
        return predicate;
      }
      else
      {
        throw new ArgumentNullException($"Internal Server Error: The {nameof(numberValue)} property of {nameof(numberValue.Value)} was found to be null.");
      }
    }

    private Expression<Func<IndexQuantity, bool>> GreaterThanOrEqualTo(SearchQueryNumberValue numberValue)
    {
      var predicate = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      if (numberValue.Value.HasValue && numberValue.Scale.HasValue)
      {
        predicate = predicate.And(NumberGreaterThanOrEqualTo(numberValue.Value.Value));
        return predicate;
      }
      else
      {
        throw new ArgumentNullException($"Internal Server Error: The {nameof(numberValue)} property of {nameof(numberValue.Value)} was found to be null.");
      }
    }

    private Expression<Func<IndexQuantity, bool>> LessThan(SearchQueryNumberValue numberValue)
    {
      var predicate = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      if (numberValue.Value.HasValue && numberValue.Scale.HasValue)
      {
        predicate = predicate.And(NumberLessThen(numberValue.Value.Value));
        return predicate;
      }
      else
      {
        throw new ArgumentNullException($"Internal Server Error: The {nameof(numberValue)} property of {nameof(numberValue.Value)} was found to be null.");
      }
    }

    private Expression<Func<IndexQuantity, bool>> LessThanOrEqualTo(SearchQueryNumberValue numberValue)
    {
      var predicate = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      if (numberValue.Value.HasValue && numberValue.Scale.HasValue)
      {
        predicate = predicate.And(NumberLessThanOrEqualTo(numberValue.Value.Value));
        return predicate;
      }
      else
      {
        throw new ArgumentNullException($"Internal Server Error: The {nameof(numberValue)} property of {nameof(numberValue.Value)} was found to be null.");
      }
    }


    private Expression<Func<ResourceStore, bool>> AnyIndex(Expression<Func<IndexQuantity, bool>> predicate)
    {
      return x => x.IndexQuantityList.Any(predicate.Compile());
    }
    private Expression<Func<ResourceStore, bool>> AnyIndexEquals(Expression<Func<IndexQuantity, bool>> predicate, bool equals)
    {
      return x => x.IndexQuantityList.Any(predicate.Compile()) == equals;
    }
    private Expression<Func<IndexQuantity, bool>> IsSearchParameterId(int searchParameterId)
    {
      return x => x.SearchParameterStoreId == searchParameterId;
    }
    private Expression<Func<IndexQuantity, bool>> IsNotSearchParameterId(int searchParameterId)
    {
      return x => x.SearchParameterStoreId != searchParameterId;
    }

    private Expression<Func<IndexQuantity, bool>> NumberEqualTo(decimal midValue, int scale)
    {
      var predicateMain = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      var lowValue = DecimalSupport.CalculateLowNumber(midValue, scale);
      var highValue = DecimalSupport.CalculateHighNumber(midValue, scale);

      //PredicateOne: x => x.Number >= lowValue && x.Number <= highValue && x.Comparator == null      
      var predicateOne = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateOne = predicateOne.And(IndexDecimal_IsHigherThanOrEqualTo(lowValue));
      predicateOne = predicateOne.And(IndexDecimal_IsLowerThanOrEqualTo(highValue));
      predicateOne = predicateOne.And(ComparatorIsNull());

      //PredicateTwo: x => x.Number <= midValue && x.Comparator == GreaterOrEqual  
      var predicateTwo = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateTwo = predicateTwo.And(IndexDecimal_IsLowerThanOrEqualTo(midValue));
      predicateTwo = predicateTwo.And(ComparatorIsEqualTo(QuantityComparator.GreaterOrEqual));

      //PredicateThree: x => x.Number >= midValue && x.Comparator == LessOrEqual  
      var predicateThree = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateThree = predicateThree.And(IndexDecimal_IsHigherThanOrEqualTo(midValue));
      predicateThree = predicateThree.And(ComparatorIsEqualTo(QuantityComparator.LessOrEqual));

      //PredicateThree: x => x.Number > midValue && x.Comparator == LessOrEqual  
      var predicateFour = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateFour = predicateFour.And(IndexDecimal_IsHigherThan(midValue));
      predicateFour = predicateFour.And(ComparatorIsEqualTo(QuantityComparator.LessOrEqual));

      //PredicateThree: x => x.Number < midValue && x.Comparator == GreaterThan  
      var predicateFive = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateFive = predicateFive.And(IndexDecimal_IsLowerThan(midValue));
      predicateFive = predicateFive.And(ComparatorIsEqualTo(QuantityComparator.GreaterThan));

      predicateMain = predicateMain.Or(predicateOne);
      predicateMain = predicateMain.Or(predicateTwo);
      predicateMain = predicateMain.Or(predicateThree);
      predicateMain = predicateMain.Or(predicateFour);
      predicateMain = predicateMain.Or(predicateFive);

      return predicateMain;
    }

    private Expression<Func<IndexQuantity, bool>> NumberNotEqualTo(decimal midValue, int scale)
    {
      var predicateMain = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      var lowValue = DecimalSupport.CalculateLowNumber(midValue, scale);
      var highValue = DecimalSupport.CalculateHighNumber(midValue, scale);

      //PredicateOne: x => (x.Number < lowValue || x.Number > highValue) && x.Comparator == null            
      var predicateOne = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      var subOr = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      subOr = subOr.Or(IndexDecimal_IsLowerThan(lowValue));
      subOr = subOr.Or(IndexDecimal_IsHigherThan(highValue));
      predicateOne = predicateOne.And(subOr);
      predicateOne = predicateOne.And(ComparatorIsNull());

      //PredicateTwo: x => x.Number <= midValue && x.Comparator == GreaterOrEqual  
      var predicateTwo = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateTwo = predicateTwo.And(IndexDecimal_IsHigherThan(midValue));
      predicateTwo = predicateTwo.And(ComparatorIsEqualTo(QuantityComparator.GreaterOrEqual));

      //PredicateThree: x => x.Number >= midValue && x.Comparator == LessOrEqual  
      var predicateThree = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateThree = predicateThree.And(IndexDecimal_IsHigherThanOrEqualTo(midValue));
      predicateThree = predicateThree.And(ComparatorIsEqualTo(QuantityComparator.GreaterThan));

      //PredicateThree: x => x.Number > midValue && x.Comparator == LessOrEqual  
      var predicateFour = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateFour = predicateFour.And(IndexDecimal_IsLowerThan(midValue));
      predicateFour = predicateFour.And(ComparatorIsEqualTo(QuantityComparator.LessOrEqual));

      //PredicateThree: x => x.Number < midValue && x.Comparator == GreaterThan  
      var predicateFive = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateFive = predicateFive.And(IndexDecimal_IsLowerThanOrEqualTo(midValue));
      predicateFive = predicateFive.And(ComparatorIsEqualTo(QuantityComparator.LessThan));

      predicateMain = predicateMain.Or(predicateOne);
      predicateMain = predicateMain.Or(predicateTwo);
      predicateMain = predicateMain.Or(predicateThree);
      predicateMain = predicateMain.Or(predicateFour);
      predicateMain = predicateMain.Or(predicateFive);

      return predicateMain;
    }

    private Expression<Func<IndexQuantity, bool>> NumberGreaterThen(decimal midValue)
    {
      var predicateMain = LinqKit.PredicateBuilder.New<IndexQuantity>(true);

      //PredicateOne: x => x.Number > midValue && x.Comparator == null      
      var predicateOne = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateOne = predicateOne.And(IndexDecimal_IsHigherThan(midValue));
      predicateOne = predicateOne.And(ComparatorIsNull());

      //PredicateTwo: x => x.Comparator == GreaterOrEqual || x.Comparator == GreaterThan 
      var predicateTwo = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateTwo = predicateTwo.Or(ComparatorIsEqualTo(QuantityComparator.GreaterOrEqual));
      predicateTwo = predicateTwo.Or(ComparatorIsEqualTo(QuantityComparator.GreaterThan));

      //PredicateThree: x => x.Number > midValue && x.Comparator == LessOrEqual  
      var predicateThree = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateThree = predicateThree.And(IndexDecimal_IsHigherThan(midValue));
      predicateThree = predicateThree.And(ComparatorIsEqualTo(QuantityComparator.LessOrEqual));

      //PredicateFour: x => x.Number > midValue && x.Comparator == LessThan  
      var predicateFour = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateFour = predicateFour.And(IndexDecimal_IsHigherThan(midValue));
      predicateFour = predicateFour.And(ComparatorIsEqualTo(QuantityComparator.LessThan));

      predicateMain = predicateMain.Or(predicateOne);
      predicateMain = predicateMain.Or(predicateTwo);
      predicateMain = predicateMain.Or(predicateThree);
      predicateMain = predicateMain.Or(predicateFour);

      return predicateMain;
    }

    private Expression<Func<IndexQuantity, bool>> NumberGreaterThanOrEqualTo(decimal midValue)
    {
      var predicateMain = LinqKit.PredicateBuilder.New<IndexQuantity>(true);

      //PredicateOne: x => x.Number > midValue && x.Comparator == null      
      var predicateOne = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateOne = predicateOne.And(IndexDecimal_IsHigherThanOrEqualTo(midValue));
      predicateOne = predicateOne.And(ComparatorIsNull());

      //PredicateTwo: x => x.Comparator == GreaterOrEqual || x.Comparator == GreaterThan 
      var predicateTwo = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateTwo = predicateTwo.Or(ComparatorIsEqualTo(QuantityComparator.GreaterOrEqual));
      predicateTwo = predicateTwo.Or(ComparatorIsEqualTo(QuantityComparator.GreaterThan));

      //PredicateThree: x => x.Number > midValue && x.Comparator == LessOrEqual  
      var predicateThree = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateThree = predicateThree.And(IndexDecimal_IsHigherThanOrEqualTo(midValue));
      predicateThree = predicateThree.And(ComparatorIsEqualTo(QuantityComparator.LessOrEqual));

      //PredicateFour: x => x.Number > midValue && x.Comparator == LessThan  
      var predicateFour = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateFour = predicateFour.And(IndexDecimal_IsHigherThan(midValue));
      predicateFour = predicateFour.And(ComparatorIsEqualTo(QuantityComparator.LessThan));

      predicateMain = predicateMain.Or(predicateOne);
      predicateMain = predicateMain.Or(predicateTwo);
      predicateMain = predicateMain.Or(predicateThree);
      predicateMain = predicateMain.Or(predicateFour);

      return predicateMain;
    }

    private Expression<Func<IndexQuantity, bool>> NumberLessThen(decimal midValue)
    {
      var predicateMain = LinqKit.PredicateBuilder.New<IndexQuantity>(true);

      //PredicateOne: x => x.Number > midValue && x.Comparator == null      
      var predicateOne = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateOne = predicateOne.And(IndexDecimal_IsLowerThan(midValue));
      predicateOne = predicateOne.And(ComparatorIsNull());

      //PredicateTwo: x => x.Comparator == GreaterOrEqual || x.Comparator == GreaterThan 
      var predicateTwo = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateTwo = predicateTwo.Or(ComparatorIsEqualTo(QuantityComparator.LessOrEqual));
      predicateTwo = predicateTwo.Or(ComparatorIsEqualTo(QuantityComparator.LessThan));

      //PredicateThree: x => x.Number > midValue && x.Comparator == LessOrEqual  
      var predicateThree = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateThree = predicateThree.And(IndexDecimal_IsLowerThan(midValue));
      predicateThree = predicateThree.And(ComparatorIsEqualTo(QuantityComparator.GreaterOrEqual));

      //PredicateFour: x => x.Number > midValue && x.Comparator == LessThan  
      var predicateFour = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateFour = predicateFour.And(IndexDecimal_IsLowerThan(midValue));
      predicateFour = predicateFour.And(ComparatorIsEqualTo(QuantityComparator.GreaterThan));

      predicateMain = predicateMain.Or(predicateOne);
      predicateMain = predicateMain.Or(predicateTwo);
      predicateMain = predicateMain.Or(predicateThree);
      predicateMain = predicateMain.Or(predicateFour);

      return predicateMain;
    }

    private Expression<Func<IndexQuantity, bool>> NumberLessThanOrEqualTo(decimal midValue)
    {
      var predicateMain = LinqKit.PredicateBuilder.New<IndexQuantity>(true);

      //PredicateOne: x => x.Number > midValue && x.Comparator == null      
      var predicateOne = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateOne = predicateOne.And(IndexDecimal_IsLowerThanOrEqualTo(midValue));
      predicateOne = predicateOne.And(ComparatorIsNull());

      //PredicateTwo: x => x.Comparator == GreaterOrEqual || x.Comparator == GreaterThan 
      var predicateTwo = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateTwo = predicateTwo.Or(ComparatorIsEqualTo(QuantityComparator.LessOrEqual));
      predicateTwo = predicateTwo.Or(ComparatorIsEqualTo(QuantityComparator.LessThan));

      //PredicateThree: x => x.Number > midValue && x.Comparator == LessOrEqual  
      var predicateThree = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateThree = predicateThree.And(IndexDecimal_IsLowerThanOrEqualTo(midValue));
      predicateThree = predicateThree.And(ComparatorIsEqualTo(QuantityComparator.GreaterOrEqual));

      //PredicateFour: x => x.Number > midValue && x.Comparator == LessThan  
      var predicateFour = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      predicateFour = predicateFour.And(IndexDecimal_IsLowerThan(midValue));
      predicateFour = predicateFour.And(ComparatorIsEqualTo(QuantityComparator.GreaterThan));

      predicateMain = predicateMain.Or(predicateOne);
      predicateMain = predicateMain.Or(predicateTwo);
      predicateMain = predicateMain.Or(predicateThree);
      predicateMain = predicateMain.Or(predicateFour);

      return predicateMain;
    }


    private Expression<Func<IndexQuantity, bool>> ComparatorIsNull()
    {
      return x => x.Comparator == null;
    }

    private Expression<Func<IndexQuantity, bool>> ComparatorIsEqualTo(QuantityComparator? quantityComparator)
    {
      return x => x.Comparator == quantityComparator;
    }

    private static Expression<Func<IndexQuantity, bool>> IndexDecimal_IsHigherThanOrEqualTo(decimal value)
    {
      return x => x.Quantity >= value;
    }

    private Expression<Func<IndexQuantity, bool>> IndexDecimal_IsHigherThan(decimal value)
    {
      return x => x.Quantity > value;
    }

    private Expression<Func<IndexQuantity, bool>> IndexDecimal_IsLowerThanOrEqualTo(decimal value)
    {
      return x => x.Quantity <= value;
    }

    private Expression<Func<IndexQuantity, bool>> IndexDecimal_IsLowerThan(decimal value)
    {
      return x => x.Quantity < value;
    }
  }


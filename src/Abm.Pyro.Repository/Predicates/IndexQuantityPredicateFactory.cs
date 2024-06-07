using System.Linq.Expressions;
using Hl7.Fhir.Model;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Repository.Predicates;

public class IndexQuantityPredicateFactory : IIndexQuantityPredicateFactory
{
  public List<Expression<Func<IndexQuantity, bool>>> QuantityIndex(SearchQueryQuantity searchQueryQuantity)
  {
    var resultList = new List<Expression<Func<IndexQuantity, bool>>>();

    foreach (SearchQueryQuantityValue quantityValue in searchQueryQuantity.ValueList)
    {
      if (!searchQueryQuantity.SearchParameter.SearchParameterStoreId.HasValue)
      {
        throw new ArgumentNullException(nameof(searchQueryQuantity.SearchParameter.SearchParameterStoreId));
      }

      var indexQuantityPredicate = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
      indexQuantityPredicate = indexQuantityPredicate.And(IsSearchParameterId(searchQueryQuantity.SearchParameter.SearchParameterStoreId.Value));

      if (!searchQueryQuantity.Modifier.HasValue)
      {
        if (!quantityValue.Prefix.HasValue)
        {
          indexQuantityPredicate = indexQuantityPredicate.And(EqualTo(quantityValue));
          resultList.Add(indexQuantityPredicate);
        }
        else
        {
          var arrayOfSupportedPrefixes = FhirSearchQuerySupport.GetPrefixListForSearchType(searchQueryQuantity.SearchParameter.Type);
          if (arrayOfSupportedPrefixes.Contains(quantityValue.Prefix.Value))
          {
            switch (quantityValue.Prefix.Value)
            {
              case SearchComparatorId.Eq:
                indexQuantityPredicate = indexQuantityPredicate.And(EqualTo(quantityValue));
                resultList.Add(indexQuantityPredicate);
                break;
              case SearchComparatorId.Ne:
                indexQuantityPredicate = indexQuantityPredicate.And(NotEqualTo(quantityValue));
                resultList.Add(indexQuantityPredicate);
                break;
              case SearchComparatorId.Gt:
                indexQuantityPredicate = indexQuantityPredicate.And(GreaterThan(quantityValue));
                resultList.Add(indexQuantityPredicate);
                break;
              case SearchComparatorId.Lt:
                indexQuantityPredicate = indexQuantityPredicate.And(LessThan(quantityValue));
                resultList.Add(indexQuantityPredicate);
                break;
              case SearchComparatorId.Ge:
                indexQuantityPredicate = indexQuantityPredicate.And(GreaterThanOrEqualTo(quantityValue));
                resultList.Add(indexQuantityPredicate);
                break;
              case SearchComparatorId.Le:
                indexQuantityPredicate = indexQuantityPredicate.And(LessThanOrEqualTo(quantityValue));
                resultList.Add(indexQuantityPredicate);
                break;
              default:
                throw new System.ComponentModel.InvalidEnumArgumentException(quantityValue.Prefix.Value.GetCode(), (int)quantityValue.Prefix.Value, typeof(SearchParameter.SearchComparator));
            }
          }
          else
          {
            string supportedPrefixes = String.Join(',', arrayOfSupportedPrefixes);
            throw new ApplicationException($"Internal Server Error: The search query prefix: {quantityValue.Prefix.Value.GetCode()} is not supported for search parameter types of: {searchQueryQuantity.SearchParameter.Type.GetCode()}. The supported prefixes are: {supportedPrefixes}");
          }
        }
      }
      else
      {
        var arrayOfSupportedModifiers = FhirSearchQuerySupport.GetModifiersForSearchType(searchQueryQuantity.SearchParameter.Type);
        if (arrayOfSupportedModifiers.Contains(searchQueryQuantity.Modifier.Value))
        {
          if (searchQueryQuantity.Modifier.Value != SearchModifierCodeId.Missing)
          {
            if (quantityValue.Value is null)
            {
              throw new ArgumentNullException(nameof(quantityValue.Value));
            }
          }
          switch (searchQueryQuantity.Modifier.Value)
          {
            case SearchModifierCodeId.Missing:
              if (quantityValue.Prefix.HasValue == false)
              {
                indexQuantityPredicate = indexQuantityPredicate.And(IsNotSearchParameterId(searchQueryQuantity.SearchParameter.SearchParameterStoreId.Value));
                resultList.Add(indexQuantityPredicate);
              }
              else
              {
                throw new ApplicationException($"Internal Server Error: Encountered a Quantity Query with a: {SearchModifierCodeId.Missing.GetCode()} modifier and a prefix of {quantityValue.Prefix!.GetCode()}. This should not happen as a missing parameter value must be only True or False with no prefix.");
              }
              break;
            default:
              throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryQuantity.Modifier.Value.GetCode()} has been added to the supported list for {searchQueryQuantity.SearchParameter.Type.GetCode()} search parameter queries and yet no database predicate has been provided.");
          }
        }
        else
        {
          throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryQuantity.Modifier.Value.GetCode()} is not supported for search parameter types of {searchQueryQuantity.SearchParameter.Type.GetCode()}.");
        }
      }
    }
    return resultList;
  }

  private Expression<Func<IndexQuantity, bool>> EqualTo(SearchQueryQuantityValue quantityValue)
  {
    var predicate = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    if (quantityValue.Value.HasValue && quantityValue.Scale.HasValue)
    {
      if (quantityValue.Code is null)
      {
        predicate = predicate.And(QuantityEqualTo(quantityValue.Value.Value, quantityValue.Scale.Value));
        return predicate;
      }
      predicate = predicate.And(SystemCodeOrCodeUnitEqualTo(quantityValue.System, quantityValue.Code));
      predicate = predicate.And(QuantityEqualTo(quantityValue.Value.Value, quantityValue.Scale.Value));
      return predicate;
    }
    throw new ArgumentNullException($"Internal Server Error: The {nameof(quantityValue)} property of {nameof(quantityValue.Value)} was found to be null.");
  }

  private Expression<Func<IndexQuantity, bool>> NotEqualTo(SearchQueryQuantityValue quantityValue)
  {
    var predicate = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    if (quantityValue.Value.HasValue && quantityValue.Scale.HasValue)
    {
      if (quantityValue.Code is null)
      {
        predicate = predicate.Or(QuantityNotEqualTo(quantityValue.Value.Value, quantityValue.Scale.Value));
        return predicate;
      }
      predicate = predicate.Or(SystemCodeOrCodeUnitNotEqualTo(quantityValue.System, quantityValue.Code));
      predicate = predicate.Or(QuantityNotEqualTo(quantityValue.Value.Value, quantityValue.Scale.Value));
      return predicate;
    }

    throw new ArgumentNullException($"Internal Server Error: The {nameof(quantityValue)} property of {nameof(quantityValue.Value)} was found to be null.");
  }

  private Expression<Func<IndexQuantity, bool>> GreaterThan(SearchQueryQuantityValue quantityValue)
  {
    var predicate = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    if (quantityValue.Value.HasValue && quantityValue.Scale.HasValue)
    {
      if (quantityValue.Code is null)
      {
        predicate = predicate.And(QuantityGreaterThen(quantityValue.Value.Value));
        return predicate;
      }

      predicate = predicate.And(SystemCodeOrCodeUnitEqualTo(quantityValue.System, quantityValue.Code));
      predicate = predicate.And(QuantityGreaterThen(quantityValue.Value.Value));
      return predicate;
    }

    throw new ArgumentNullException($"Internal Server Error: The {nameof(quantityValue)} property of {nameof(quantityValue.Value)} was found to be null.");
  }

  private Expression<Func<IndexQuantity, bool>> GreaterThanOrEqualTo(SearchQueryQuantityValue quantityValue)
  {
    var predicate = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    if (quantityValue.Value.HasValue && quantityValue.Scale.HasValue)
    {
      if (quantityValue.Code is null)
      {
        predicate = predicate.And(QuantityGreaterThanOrEqualTo(quantityValue.Value.Value));
        return predicate;
      }
      predicate = predicate.And(SystemCodeOrCodeUnitEqualTo(quantityValue.System, quantityValue.Code));
      predicate = predicate.And(QuantityGreaterThanOrEqualTo(quantityValue.Value.Value));
      return predicate;
    }
    throw new ArgumentNullException($"Internal Server Error: The {nameof(quantityValue)} property of {nameof(quantityValue.Value)} was found to be null.");
  }

  private Expression<Func<IndexQuantity, bool>> LessThan(SearchQueryQuantityValue quantityValue)
  {
    var predicate = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    if (quantityValue.Value.HasValue && quantityValue.Scale.HasValue)
    {
      if (quantityValue.Code is null)
      {
        predicate = predicate.And(QuantityLessThen(quantityValue.Value.Value));
        return predicate;
      }
      predicate = predicate.And(SystemCodeOrCodeUnitEqualTo(quantityValue.System, quantityValue.Code));
      predicate = predicate.And(QuantityLessThen(quantityValue.Value.Value));
      return predicate;
    }
    throw new ArgumentNullException($"Internal Server Error: The {nameof(quantityValue)} property of {nameof(quantityValue.Value)} was found to be null.");
  }

  private Expression<Func<IndexQuantity, bool>> LessThanOrEqualTo(SearchQueryQuantityValue quantityValue)
  {
    var predicate = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    if (quantityValue.Value.HasValue && quantityValue.Scale.HasValue)
    {
      if (quantityValue.Code is null)
      {
        predicate = predicate.And(QuantityLessThanOrEqualTo(quantityValue.Value.Value));
        return predicate;
      }
      predicate = predicate.And(SystemCodeOrCodeUnitEqualTo(quantityValue.System, quantityValue.Code));
      predicate = predicate.And(QuantityLessThanOrEqualTo(quantityValue.Value.Value));
      return predicate;
    }
    throw new ArgumentNullException($"Internal Server Error: The {nameof(quantityValue)} property of {nameof(quantityValue.Value)} was found to be null.");
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

  private Expression<Func<IndexQuantity, bool>> QuantityEqualTo(decimal midValue, int scale)
  {
    var predicateMain = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    var lowValue = DecimalSupport.CalculateLowNumber(midValue, scale);
    var highValue = DecimalSupport.CalculateHighNumber(midValue, scale);

    //PredicateOne: x => x.Quantity >= lowValue && x.Quantity <= highValue && x.Comparator == null      
    var predicateOne = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateOne = predicateOne.And(IndexDecimal_IsHigherThanOrEqualTo(lowValue));
    predicateOne = predicateOne.And(IndexDecimal_IsLowerThanOrEqualTo(highValue));
    predicateOne = predicateOne.And(ComparatorIsNull());

    //PredicateTwo: x => x.Quantity <= midValue && x.Comparator == GreaterOrEqual  
    var predicateTwo = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateTwo = predicateTwo.And(IndexDecimal_IsLowerThanOrEqualTo(midValue));
    predicateTwo = predicateTwo.And(ComparatorIsEqualTo(QuantityComparator.GreaterOrEqual));

    //PredicateThree: x => x.Quantity >= midValue && x.Comparator == LessOrEqual  
    var predicateThree = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateThree = predicateThree.And(IndexDecimal_IsHigherThanOrEqualTo(midValue));
    predicateThree = predicateThree.And(ComparatorIsEqualTo(QuantityComparator.LessOrEqual));

    //PredicateThree: x => x.Quantity > midValue && x.Comparator == LessOrEqual  
    var predicateFour = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateFour = predicateFour.And(IndexDecimal_IsHigherThan(midValue));
    predicateFour = predicateFour.And(ComparatorIsEqualTo(QuantityComparator.LessOrEqual));

    //PredicateThree: x => x.Quantity < midValue && x.Comparator == GreaterThan  
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
  private Expression<Func<IndexQuantity, bool>> QuantityNotEqualTo(decimal midValue, int scale)
  {
    var predicateMain = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    var lowValue = DecimalSupport.CalculateLowNumber(midValue, scale);
    var highValue = DecimalSupport.CalculateHighNumber(midValue, scale);

    //PredicateOne: x => (x.Quantity < lowValue || x.Quantity > highValue) && x.Comparator == null            
    var predicateOne = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    var subOr = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    subOr = subOr.Or(IndexDecimal_IsLowerThan(lowValue));
    subOr = subOr.Or(IndexDecimal_IsHigherThan(highValue));
    predicateOne = predicateOne.And(subOr);
    predicateOne = predicateOne.And(ComparatorIsNull());

    //PredicateTwo: x => x.Quantity <= midValue && x.Comparator == GreaterOrEqual  
    var predicateTwo = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateTwo = predicateTwo.And(IndexDecimal_IsHigherThan(midValue));
    predicateTwo = predicateTwo.And(ComparatorIsEqualTo(QuantityComparator.GreaterOrEqual));

    //PredicateThree: x => x.Quantity >= midValue && x.Comparator == LessOrEqual  
    var predicateThree = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateThree = predicateThree.And(IndexDecimal_IsHigherThanOrEqualTo(midValue));
    predicateThree = predicateThree.And(ComparatorIsEqualTo(QuantityComparator.GreaterThan));

    //PredicateThree: x => x.Quantity > midValue && x.Comparator == LessOrEqual  
    var predicateFour = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateFour = predicateFour.And(IndexDecimal_IsLowerThan(midValue));
    predicateFour = predicateFour.And(ComparatorIsEqualTo(QuantityComparator.LessOrEqual));

    //PredicateThree: x => x.Quantity < midValue && x.Comparator == GreaterThan  
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

  private Expression<Func<IndexQuantity, bool>> QuantityGreaterThen(decimal midValue)
  {
    var predicateMain = LinqKit.PredicateBuilder.New<IndexQuantity>(true);

    //PredicateOne: x => x.Quantity > midValue && x.Comparator == null      
    var predicateOne = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateOne = predicateOne.And(IndexDecimal_IsHigherThan(midValue));
    predicateOne = predicateOne.And(ComparatorIsNull());

    //PredicateTwo: x => x.Comparator == GreaterOrEqual || x.Comparator == GreaterThan 
    var predicateTwo = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateTwo = predicateTwo.Or(ComparatorIsEqualTo(QuantityComparator.GreaterOrEqual));
    predicateTwo = predicateTwo.Or(ComparatorIsEqualTo(QuantityComparator.GreaterThan));

    //PredicateThree: x => x.Quantity > midValue && x.Comparator == LessOrEqual  
    var predicateThree = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateThree = predicateThree.And(IndexDecimal_IsHigherThan(midValue));
    predicateThree = predicateThree.And(ComparatorIsEqualTo(QuantityComparator.LessOrEqual));

    //PredicateFour: x => x.Quantity > midValue && x.Comparator == LessThan  
    var predicateFour = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateFour = predicateFour.And(IndexDecimal_IsHigherThan(midValue));
    predicateFour = predicateFour.And(ComparatorIsEqualTo(QuantityComparator.LessThan));

    predicateMain = predicateMain.Or(predicateOne);
    predicateMain = predicateMain.Or(predicateTwo);
    predicateMain = predicateMain.Or(predicateThree);
    predicateMain = predicateMain.Or(predicateFour);

    return predicateMain;
  }
  private Expression<Func<IndexQuantity, bool>> QuantityGreaterThanOrEqualTo(decimal midValue)
  {
    var predicateMain = LinqKit.PredicateBuilder.New<IndexQuantity>(true);

    //PredicateOne: x => x.Quantity > midValue && x.Comparator == null      
    var predicateOne = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateOne = predicateOne.And(IndexDecimal_IsHigherThanOrEqualTo(midValue));
    predicateOne = predicateOne.And(ComparatorIsNull());

    //PredicateTwo: x => x.Comparator == GreaterOrEqual || x.Comparator == GreaterThan 
    var predicateTwo = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateTwo = predicateTwo.Or(ComparatorIsEqualTo(QuantityComparator.GreaterOrEqual));
    predicateTwo = predicateTwo.Or(ComparatorIsEqualTo(QuantityComparator.GreaterThan));

    //PredicateThree: x => x.Quantity > midValue && x.Comparator == LessOrEqual  
    var predicateThree = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateThree = predicateThree.And(IndexDecimal_IsHigherThanOrEqualTo(midValue));
    predicateThree = predicateThree.And(ComparatorIsEqualTo(QuantityComparator.LessOrEqual));

    //PredicateFour: x => x.Quantity > midValue && x.Comparator == LessThan  
    var predicateFour = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateFour = predicateFour.And(IndexDecimal_IsHigherThan(midValue));
    predicateFour = predicateFour.And(ComparatorIsEqualTo(QuantityComparator.LessThan));

    predicateMain = predicateMain.Or(predicateOne);
    predicateMain = predicateMain.Or(predicateTwo);
    predicateMain = predicateMain.Or(predicateThree);
    predicateMain = predicateMain.Or(predicateFour);

    return predicateMain;
  }
  private Expression<Func<IndexQuantity, bool>> QuantityLessThen(decimal midValue)
  {
    var predicateMain = LinqKit.PredicateBuilder.New<IndexQuantity>(true);

    //PredicateOne: x => x.Quantity > midValue && x.Comparator == null      
    var predicateOne = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateOne = predicateOne.And(IndexDecimal_IsLowerThan(midValue));
    predicateOne = predicateOne.And(ComparatorIsNull());

    //PredicateTwo: x => x.Comparator == GreaterOrEqual || x.Comparator == GreaterThan 
    var predicateTwo = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateTwo = predicateTwo.Or(ComparatorIsEqualTo(QuantityComparator.LessOrEqual));
    predicateTwo = predicateTwo.Or(ComparatorIsEqualTo(QuantityComparator.LessThan));

    //PredicateThree: x => x.Quantity > midValue && x.Comparator == LessOrEqual  
    var predicateThree = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateThree = predicateThree.And(IndexDecimal_IsLowerThan(midValue));
    predicateThree = predicateThree.And(ComparatorIsEqualTo(QuantityComparator.GreaterOrEqual));

    //PredicateFour: x => x.Quantity > midValue && x.Comparator == LessThan  
    var predicateFour = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateFour = predicateFour.And(IndexDecimal_IsLowerThan(midValue));
    predicateFour = predicateFour.And(ComparatorIsEqualTo(QuantityComparator.GreaterThan));

    predicateMain = predicateMain.Or(predicateOne);
    predicateMain = predicateMain.Or(predicateTwo);
    predicateMain = predicateMain.Or(predicateThree);
    predicateMain = predicateMain.Or(predicateFour);

    return predicateMain;
  }
  private Expression<Func<IndexQuantity, bool>> QuantityLessThanOrEqualTo(decimal midValue)
  {
    var predicateMain = LinqKit.PredicateBuilder.New<IndexQuantity>(true);

    //PredicateOne: x => x.Quantity > midValue && x.Comparator == null      
    var predicateOne = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateOne = predicateOne.And(IndexDecimal_IsLowerThanOrEqualTo(midValue));
    predicateOne = predicateOne.And(ComparatorIsNull());

    //PredicateTwo: x => x.Comparator == GreaterOrEqual || x.Comparator == GreaterThan 
    var predicateTwo = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateTwo = predicateTwo.Or(ComparatorIsEqualTo(QuantityComparator.LessOrEqual));
    predicateTwo = predicateTwo.Or(ComparatorIsEqualTo(QuantityComparator.LessThan));

    //PredicateThree: x => x.Quantity > midValue && x.Comparator == LessOrEqual  
    var predicateThree = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateThree = predicateThree.And(IndexDecimal_IsLowerThanOrEqualTo(midValue));
    predicateThree = predicateThree.And(ComparatorIsEqualTo(QuantityComparator.GreaterOrEqual));

    //PredicateFour: x => x.Quantity > midValue && x.Comparator == LessThan  
    var predicateFour = LinqKit.PredicateBuilder.New<IndexQuantity>(true);
    predicateFour = predicateFour.And(IndexDecimal_IsLowerThan(midValue));
    predicateFour = predicateFour.And(ComparatorIsEqualTo(QuantityComparator.GreaterThan));

    predicateMain = predicateMain.Or(predicateOne);
    predicateMain = predicateMain.Or(predicateTwo);
    predicateMain = predicateMain.Or(predicateThree);
    predicateMain = predicateMain.Or(predicateFour);

    return predicateMain;
  }
  private Expression<Func<IndexQuantity, bool>> SystemCodeOrCodeUnitEqualTo(string? system, string code)
  {
    if (system is null)
    {
      return x => (x.Code == code) || (x.Unit == code);
    }
    return x => (x.System == system) && (x.Code == code);
  }

  private Expression<Func<IndexQuantity, bool>> SystemCodeOrCodeUnitNotEqualTo(string? system, string code)
  {
    if (system is null)
    {
      return x => (x.Code != code) && (x.Unit != code);
    }
    return x => (x.System != system) || (x.Code != code);
  }

  private Expression<Func<IndexQuantity, bool>> ComparatorIsNull()
  {
    return x => x.Comparator == null;
  }

  private Expression<Func<IndexQuantity, bool>> ComparatorIsEqualTo(QuantityComparator? quantityComparator)
  {
    return x => x.Comparator == quantityComparator;
  }

  private Expression<Func<IndexQuantity, bool>> IndexDecimal_IsHigherThanOrEqualTo(decimal value)
  {
    return x => x.QuantityHigh >= value;
  }

  private Expression<Func<IndexQuantity, bool>> IndexDecimal_IsHigherThan(decimal value)
  {
    return x => x.QuantityHigh > value;
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

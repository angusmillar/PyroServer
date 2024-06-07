using System.Linq.Expressions;
using Hl7.Fhir.Model;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;

namespace Abm.Pyro.Repository.Predicates;

  public class IndexDateTimePredicateFactory(IFhirDateTimeSupport fhirDateTimeSupport) : IIndexDateTimePredicateFactory
  {
    public List<Expression<Func<IndexDateTime, bool>>> DateTimeIndex(SearchQueryDateTime searchQueryDateTime)
    {
      var resultList = new List<Expression<Func<IndexDateTime, bool>>>();

      foreach (SearchQueryDateTimeValue dateTimeValue in searchQueryDateTime.ValueList)
      {
        if (!searchQueryDateTime.SearchParameter.SearchParameterStoreId.HasValue)
        {
          throw new ArgumentNullException(nameof(searchQueryDateTime.SearchParameter.SearchParameterStoreId));
        }
        
        var indexDateTimePredicate = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
        indexDateTimePredicate = indexDateTimePredicate.And(IsSearchParameterId(searchQueryDateTime.SearchParameter.SearchParameterStoreId.Value));

        if (!searchQueryDateTime.Modifier.HasValue)
        {
          if (!dateTimeValue.Value.HasValue || !dateTimeValue.Precision.HasValue)
          {
            throw new ArgumentNullException($"Internal Server Error: Either the {nameof(dateTimeValue.Value)} or the {nameof(dateTimeValue.Precision)} of a DateTime SearchQuery is null and yet there is no missing modifier.");
          }

          if (!dateTimeValue.Prefix.HasValue)
          {
            indexDateTimePredicate = indexDateTimePredicate.And(EqualTo(dateTimeValue.Value.Value, fhirDateTimeSupport.SearchQueryCalculateHighDateTimeForRange(dateTimeValue.Value.Value, dateTimeValue.Precision.Value)));
            resultList.Add(indexDateTimePredicate);
            //ResourceStorePredicate = ResourceStorePredicate.Or(AnyIndex(IndexDateTimePredicate));
          }
          else
          {
            var arrayOfSupportedPrefixes = FhirSearchQuerySupport.GetPrefixListForSearchType(searchQueryDateTime.SearchParameter.Type);
            if (arrayOfSupportedPrefixes.Contains(dateTimeValue.Prefix.Value))
            {
              switch (dateTimeValue.Prefix.Value)
              {
                case SearchComparatorId.Eq:
                  indexDateTimePredicate = indexDateTimePredicate.And(EqualTo(dateTimeValue.Value.Value, fhirDateTimeSupport.SearchQueryCalculateHighDateTimeForRange(dateTimeValue.Value.Value, dateTimeValue.Precision.Value)));
                  resultList.Add(indexDateTimePredicate);
                  break;
                case SearchComparatorId.Ne:
                  indexDateTimePredicate = indexDateTimePredicate.And(NotEqualTo(dateTimeValue.Value.Value, fhirDateTimeSupport.SearchQueryCalculateHighDateTimeForRange(dateTimeValue.Value.Value, dateTimeValue.Precision.Value)));
                  resultList.Add(indexDateTimePredicate);
                  var searchQueryDateTimeIdPredicate = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
                  searchQueryDateTimeIdPredicate = searchQueryDateTimeIdPredicate.And(IsNotSearchParameterId(searchQueryDateTime.SearchParameter.SearchParameterStoreId.Value));
                  resultList.Add(searchQueryDateTimeIdPredicate);
                  break;
                case SearchComparatorId.Gt:
                  indexDateTimePredicate = indexDateTimePredicate.And(GreaterThan(dateTimeValue.Value.Value, fhirDateTimeSupport.SearchQueryCalculateHighDateTimeForRange(dateTimeValue.Value.Value, dateTimeValue.Precision.Value)));
                  resultList.Add(indexDateTimePredicate);
                  break;
                case SearchComparatorId.Lt:
                  indexDateTimePredicate = indexDateTimePredicate.And(LessThan(dateTimeValue.Value.Value, fhirDateTimeSupport.SearchQueryCalculateHighDateTimeForRange(dateTimeValue.Value.Value, dateTimeValue.Precision.Value)));
                  resultList.Add(indexDateTimePredicate);
                  break;
                case SearchComparatorId.Ge:
                  indexDateTimePredicate = indexDateTimePredicate.And(GreaterThanOrEqualTo(dateTimeValue.Value.Value, fhirDateTimeSupport.SearchQueryCalculateHighDateTimeForRange(dateTimeValue.Value.Value, dateTimeValue.Precision.Value)));
                  resultList.Add(indexDateTimePredicate);
                  break;
                case SearchComparatorId.Le:
                  indexDateTimePredicate = indexDateTimePredicate.And(LessThanOrEqualTo(dateTimeValue.Value.Value, fhirDateTimeSupport.SearchQueryCalculateHighDateTimeForRange(dateTimeValue.Value.Value, dateTimeValue.Precision.Value)));
                  resultList.Add(indexDateTimePredicate);
                  break;
                default:
                  throw new System.ComponentModel.InvalidEnumArgumentException(dateTimeValue.Prefix.Value.GetCode(), (int)dateTimeValue.Prefix.Value, typeof(SearchParameter.SearchComparator));
              }
            }
            else
            {
              string supportedPrefixes = String.Join(',', arrayOfSupportedPrefixes);
              throw new ApplicationException($"Internal Server Error: The search query prefix: {dateTimeValue.Prefix.Value.GetCode()} is not supported for search parameter types of: {searchQueryDateTime.SearchParameter.Type.GetCode()}. The supported prefixes are: {supportedPrefixes}");
            }
          }
        }
        else
        {
          var arrayOfSupportedModifiers = FhirSearchQuerySupport.GetModifiersForSearchType(searchQueryDateTime.SearchParameter.Type);
          if (arrayOfSupportedModifiers.Contains(searchQueryDateTime.Modifier.Value))
          {
            if (searchQueryDateTime.Modifier.Value != SearchModifierCodeId.Missing)
            {
              if (dateTimeValue.Value is null)
              {
                throw new ArgumentNullException(nameof(dateTimeValue.Value));
              }
            }
            switch (searchQueryDateTime.Modifier.Value)
            {
              case SearchModifierCodeId.Missing:
                if (dateTimeValue.Prefix.HasValue == false)
                {
                  indexDateTimePredicate = indexDateTimePredicate.And(IsNotSearchParameterId(searchQueryDateTime.SearchParameter.SearchParameterStoreId.Value));
                  resultList.Add(indexDateTimePredicate);
                }
                else
                {
                  throw new ApplicationException($"Internal Server Error: Encountered a DateTime Query with a: {SearchModifierCodeId.Missing.GetCode()} modifier and a prefix of {dateTimeValue.Prefix!.GetCode()}. This should not happen as a missing parameter value must be only True or False with no prefix.");
                }
                break;
              default:
                throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryDateTime.Modifier.Value.GetCode()} has been added to the supported list for {searchQueryDateTime.SearchParameter.Type.GetCode()} search parameter queries and yet no database predicate has been provided.");
            }
          }
          else
          {
            throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryDateTime.Modifier.Value.GetCode()} is not supported for search parameter types of {searchQueryDateTime.SearchParameter.Type.GetCode()}.");
          }
        }
      }
      return resultList;
    }
    private Expression<Func<ResourceStore, bool>> AnyIndex(Expression<Func<IndexDateTime, bool>> predicate)
    {
      return x => x.IndexDateTimeList.Any(predicate.Compile());
    }
    private Expression<Func<ResourceStore, bool>> AnyIndexEquals(Expression<Func<IndexDateTime, bool>> predicate, bool equals)
    {
      return x => x.IndexDateTimeList.Any(predicate.Compile()) == equals;
    }
    private Expression<Func<IndexDateTime, bool>> IsSearchParameterId(int searchParameterId)
    {
      return x => x.SearchParameterStoreId == searchParameterId;
    }
    private Expression<Func<IndexDateTime, bool>> IsNotSearchParameterId(int searchParameterId)
    {
      return x => x.SearchParameterStoreId != searchParameterId;
    }

    private Expression<Func<IndexDateTime, bool>> EqualTo(DateTime lowValue, DateTime highValue)
    {
      var predicateMain = LinqKit.PredicateBuilder.New<IndexDateTime>(true);

      //PredicateOne: x => x.High >= lowValue && x.High <= highValue       
      var predicateOne = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateOne = predicateOne.And(IndexDateTime_High_IsHigherThanOrEqualTo(lowValue));
      predicateOne = predicateOne.And(IndexDateTime_High_IsLowerThanOrEqualTo(highValue));

      //PredicateTwo: x => x.High >= HighValue &&  x.Low <= LowValue
      var predicateTwo = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateTwo = predicateTwo.And(IndexDateTime_High_IsHigherThanOrEqualTo(highValue));
      predicateTwo = predicateTwo.And(IndexDateTime_Low_IsLowerThanOrEqualTo(lowValue));

      //PredicateThree: x => x.Low >= LowValue && x.Low >= HighValue 
      var predicateThree = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateThree = predicateThree.And(IndexDateTime_Low_IsHigherThanOrEqualTo(lowValue));
      predicateThree = predicateThree.And(IndexDateTime_Low_IsLowerThanOrEqualTo(highValue));

      //PredicateFour: x => x.Low >= LowValue && x.High <= HighValue
      var predicateFour = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateFour = predicateFour.And(IndexDateTime_Low_IsHigherThanOrEqualTo(lowValue));
      predicateFour = predicateFour.And(IndexDateTime_High_IsLowerThanOrEqualTo(highValue));

      //PredicateFive: x => x.High == null < midValue && x.Low <= HighValue
      var predicateFive = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateFive = predicateFive.And(IndexDateTime_High_IsNull());
      predicateFive = predicateFive.And(IndexDateTime_Low_IsLowerThanOrEqualTo(highValue));

      //PredicateSix: x => x.Low == null && x.High >= HighValue
      var predicateSix = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateSix = predicateSix.And(IndexDateTime_Low_IsNull());
      predicateSix = predicateSix.And(IndexDateTime_High_IsHigherThanOrEqualTo(highValue));

      predicateMain = predicateMain.Or(predicateOne);
      predicateMain = predicateMain.Or(predicateTwo);
      predicateMain = predicateMain.Or(predicateThree);
      predicateMain = predicateMain.Or(predicateFour);
      predicateMain = predicateMain.Or(predicateFive);
      predicateMain = predicateMain.Or(predicateSix);
      
      return predicateMain;
    }
    private Expression<Func<IndexDateTime, bool>> NotEqualTo(DateTime lowValue, DateTime highValue)
    {
      var predicateMain = LinqKit.PredicateBuilder.New<IndexDateTime>(true);

      //PredicateOne: x => x.High == null && x.Low > HighValue
      var predicateOne = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateOne = predicateOne.And(IndexDateTime_High_IsNull());
      predicateOne = predicateOne.And(IndexDateTime_Low_IsHigherThan(highValue));

      //PredicateTwo: x => x.Low == null && x.High < LowValue  
      var predicateTwo = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateTwo = predicateTwo.And(IndexDateTime_Low_IsNull());
      predicateTwo = predicateTwo.And(IndexDateTime_High_IsLowerThen(lowValue));

      //PredicateThree: x => x.High > LowValue && x.High < HighValue  
      var predicateThree = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateThree = predicateThree.And(IndexDateTime_High_IsLowerThen(lowValue));
      predicateThree = predicateThree.And(IndexDateTime_High_IsLowerThen(highValue));

      //PredicateThree: x => x.Low > HighValue && x.Low > LowValue
      var predicateFour = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateFour = predicateFour.And(IndexDateTime_Low_IsHigherThan(highValue));
      predicateFour = predicateFour.And(IndexDateTime_Low_IsHigherThan(lowValue));

      predicateMain = predicateMain.Or(predicateOne);
      predicateMain = predicateMain.Or(predicateTwo);
      predicateMain = predicateMain.Or(predicateThree);
      predicateMain = predicateMain.Or(predicateFour);


      return predicateMain;
    }
    private Expression<Func<IndexDateTime, bool>> GreaterThan(DateTime lowValue, DateTime highValue)
    {
      var predicateMain = LinqKit.PredicateBuilder.New<IndexDateTime>(true);

      //PredicateOne: x => x.Low == null && x.High > HighValue 
      var predicateOne = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateOne = predicateOne.And(IndexDateTime_Low_IsNull());
      predicateOne = predicateOne.And(IndexDateTime_High_IsHigherThan(highValue));

      //PredicateTwo: x => x.High == null && x.Low != null
      var predicateTwo = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateTwo = predicateTwo.And(IndexDateTime_High_IsNull());
      predicateTwo = predicateTwo.And(IndexDateTime_Low_IsNotNull());

      //PredicateThree: x => x.High > HighValue
      var predicateThree = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateThree = predicateThree.And(IndexDateTime_High_IsHigherThan(highValue));

      predicateMain = predicateMain.Or(predicateOne);
      predicateMain = predicateMain.Or(predicateTwo);
      predicateMain = predicateMain.Or(predicateThree);

      return predicateMain;
    }
    private Expression<Func<IndexDateTime, bool>> GreaterThanOrEqualTo(DateTime lowValue, DateTime highValue)
    {
      var predicateMain = LinqKit.PredicateBuilder.New<IndexDateTime>(true);

      //PredicateOne: x => x.Low == null && x.High >= LowValue      
      var predicateOne = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateOne = predicateOne.And(IndexDateTime_Low_IsNull());
      predicateOne = predicateOne.And(IndexDateTime_High_IsHigherThanOrEqualTo(lowValue));

      //PredicateTwo: x => x.Low =! null && x.High >= LowValue 
      var predicateTwo = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateTwo = predicateTwo.Or(IndexDateTime_Low_IsNotNull());
      predicateTwo = predicateTwo.Or(IndexDateTime_High_IsHigherThanOrEqualTo(lowValue));

      //PredicateThree: x => x.Low != null && x.High == null 
      var predicateThree = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateThree = predicateThree.And(IndexDateTime_Low_IsNotNull());
      predicateThree = predicateThree.And(IndexDateTime_High_IsNull());

      predicateMain = predicateMain.Or(predicateOne);
      predicateMain = predicateMain.Or(predicateTwo);
      predicateMain = predicateMain.Or(predicateThree);

      return predicateMain;
    }
    private Expression<Func<IndexDateTime, bool>> LessThan(DateTime lowValue, DateTime highValue)
    {
      var predicateMain = LinqKit.PredicateBuilder.New<IndexDateTime>(true);

      //PredicateOne: x => x.Low == null && x.High < LowValue      
      var predicateOne = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateOne = predicateOne.And(IndexDateTime_Low_IsNull());
      predicateOne = predicateOne.And(IndexDateTime_High_IsLowerThen(lowValue));

      //PredicateTwo: x => x.Low =! null && x.High < LowValue 
      var predicateTwo = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateTwo = predicateTwo.And(IndexDateTime_Low_IsNotNull());
      predicateTwo = predicateTwo.And(IndexDateTime_High_IsLowerThen(lowValue));

      predicateMain = predicateMain.Or(predicateOne);
      predicateMain = predicateMain.Or(predicateTwo);

      return predicateMain;
    }
    private Expression<Func<IndexDateTime, bool>> LessThanOrEqualTo(DateTime lowValue, DateTime highValue)
    {
      var predicateMain = LinqKit.PredicateBuilder.New<IndexDateTime>(true);

      //PredicateOne: x => x.Low == null && x.High <= HighValue
      var predicateOne = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateOne = predicateOne.And(IndexDateTime_Low_IsNull());
      predicateOne = predicateOne.And(IndexDateTime_High_IsLowerThanOrEqualTo(highValue));

      //PredicateTwo: x => x.High == null || x.Low <= HighValue
      var predicateTwo = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateTwo = predicateTwo.And(IndexDateTime_High_IsNull());
      predicateTwo = predicateTwo.And(IndexDateTime_Low_IsLowerThanOrEqualTo(highValue));

      //PredicateThree: x => x.Low <= LowValue
      var predicateThree = LinqKit.PredicateBuilder.New<IndexDateTime>(true);
      predicateThree = predicateThree.And(IndexDateTime_Low_IsLowerThanOrEqualTo(lowValue));

      predicateMain = predicateMain.Or(predicateOne);
      predicateMain = predicateMain.Or(predicateTwo);
      predicateMain = predicateMain.Or(predicateThree);

      return predicateMain;
    }

    //High
    private Expression<Func<IndexDateTime, bool>> IndexDateTime_High_IsNull()
    {
      return x => x.HighUtc == null;
    }
    private Expression<Func<IndexDateTime, bool>> IndexDateTime_High_IsHigherThan(DateTime value)
    {
      return x => x.HighUtc > value;
    }
    private Expression<Func<IndexDateTime, bool>> IndexDateTime_High_IsHigherThanOrEqualTo(DateTime value)
    {
      return x => x.HighUtc >= value;
    }
    private Expression<Func<IndexDateTime, bool>> IndexDateTime_High_IsLowerThen(DateTime value)
    {
      return x => x.HighUtc < value;
    }
    private Expression<Func<IndexDateTime, bool>> IndexDateTime_High_IsLowerThanOrEqualTo(DateTime value)
    {
      return x => x.HighUtc <= value;
    }

    //Low
    private Expression<Func<IndexDateTime, bool>> IndexDateTime_Low_IsNull()
    {
      return x => x.LowUtc == null;
    }
    private Expression<Func<IndexDateTime, bool>> IndexDateTime_Low_IsNotNull()
    {
      return x => x.LowUtc != null;
    }
    private Expression<Func<IndexDateTime, bool>> IndexDateTime_Low_IsHigherThan(DateTime value)
    {
      return x => x.LowUtc > value;
    }
    private Expression<Func<IndexDateTime, bool>> IndexDateTime_Low_IsHigherThanOrEqualTo(DateTime value)
    {
      return x => x.LowUtc >= value;
    }
    private Expression<Func<IndexDateTime, bool>> IndexDateTime_Low_IsLowerThan(DateTime value)
    {
      return x => x.LowUtc < value;
    }
    private Expression<Func<IndexDateTime, bool>> IndexDateTime_Low_IsLowerThanOrEqualTo(DateTime value)
    {
      return x => x.LowUtc <= value;
    }

  }


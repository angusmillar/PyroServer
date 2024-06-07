using System.Text.RegularExpressions;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public abstract class SearchQueryValuePrefixBase(bool isMissing, SearchComparatorId? prefix) : SearchQueryValueBase(isMissing)
{
  public SearchComparatorId? Prefix { get; set; } = prefix;

  public static bool ValidatePreFix(SearchParamType searchParamType, SearchComparatorId? prefix)
  {
    if (!prefix.HasValue)
    {
      return true;
    }
    return Array.Exists(FhirSearchQuerySupport.GetPrefixListForSearchType(searchParamType), item => item == prefix.Value);
  }

  public static SearchComparatorId? GetPrefix(string value)
  {
    if (value.Length <= 2)
      return null;

    //Are the first two char Alpha characters 
    if (!Regex.IsMatch(value.Substring(0, 2), @"^[a-zA-Z]+$"))
      return null;

    var searchPrefixTypeDictionary = StringToEnumMap<SearchComparatorId>.GetDictionary();
    if (searchPrefixTypeDictionary.ContainsKey(value.Substring(0, 2)))
    {
      return searchPrefixTypeDictionary[value.Substring(0, 2)];
    }
    return null;
  }

  public static string RemovePrefix(string value, SearchComparatorId? prefix)
  {
    if (!prefix.HasValue)
    {
      return value;
    }
    else
    {
      if (value.Length > 2)
      {
        return value.Substring(prefix.GetCode().Length, value.Length - prefix.GetCode().Length);
      }
      else
      {
        throw new ArgumentException($"Attempt to remove the prefix {prefix.GetCode()} from the value {value} failed as the value is shorter than the prefix being removed");
      }
    }
  }

}

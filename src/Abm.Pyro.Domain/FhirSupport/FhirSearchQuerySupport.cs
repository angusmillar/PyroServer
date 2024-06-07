using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Domain.FhirSupport;

public static class FhirSearchQuerySupport
{
  public static SearchComparatorId[] GetPrefixListForSearchType(SearchParamType searchParamType)
  {
    return searchParamType switch {
      SearchParamType.Number => new SearchComparatorId[] {
                                                           SearchComparatorId.Ne,
                                                           SearchComparatorId.Eq,
                                                           SearchComparatorId.Gt,
                                                           SearchComparatorId.Ge,
                                                           SearchComparatorId.Lt,
                                                           SearchComparatorId.Le
                                                         },
      SearchParamType.Date => new SearchComparatorId[] {
                                                         SearchComparatorId.Ne,
                                                         SearchComparatorId.Eq,
                                                         SearchComparatorId.Gt,
                                                         SearchComparatorId.Ge,
                                                         SearchComparatorId.Lt,
                                                         SearchComparatorId.Le
                                                       },
      SearchParamType.String => new SearchComparatorId[] { },    //Any search parameter that's value is a string will not have prefixes
      SearchParamType.Token => new SearchComparatorId[] { },     //Any search parameter that's value is a string will not have prefixes
      SearchParamType.Reference => new SearchComparatorId[] { }, //Any search parameter that's value is a string will not have prefixes
      SearchParamType.Composite => new SearchComparatorId[] { }, //Any search parameter that's value is a string will not have prefixes
      SearchParamType.Quantity => new SearchComparatorId[] {
                                                             SearchComparatorId.Ne,
                                                             SearchComparatorId.Eq,
                                                             SearchComparatorId.Gt,
                                                             SearchComparatorId.Ge,
                                                             SearchComparatorId.Lt,
                                                             SearchComparatorId.Le
                                                           },
      SearchParamType.Uri => new SearchComparatorId[] { }, //Any search parameter that's value is a string will not have prefixes
      SearchParamType.Special => new SearchComparatorId[] { },
      _ => throw new System.ComponentModel.InvalidEnumArgumentException(searchParamType.GetCode(), (int)searchParamType, typeof(SearchParamType)),
    };
  }

  public static SearchModifierCodeId[] GetModifiersForSearchType(SearchParamType searchParamType)
  {
    return searchParamType switch {
      SearchParamType.Number => new SearchModifierCodeId[] { SearchModifierCodeId.Missing },
      SearchParamType.Date => new SearchModifierCodeId[] { SearchModifierCodeId.Missing },
      SearchParamType.String => new SearchModifierCodeId[] {
                                                             SearchModifierCodeId.Missing,
                                                             SearchModifierCodeId.Contains,
                                                             SearchModifierCodeId.Exact
                                                           },
      SearchParamType.Token => new SearchModifierCodeId[] { SearchModifierCodeId.Missing },
      //The modifiers below are supported in the spec for token but not 
      //implemented by this server as yet
      //ReturnList.Add(Conformance.SearchModifierCodeId.Text.ToString());
      //ReturnList.Add(Conformance.SearchModifierCodeId.In.ToString());
      //ReturnList.Add(Conformance.SearchModifierCodeId.Below.ToString());
      //ReturnList.Add(Conformance.SearchModifierCodeId.Above.ToString());
      //ReturnList.Add(Conformance.SearchModifierCodeId.In.ToString());
      //ReturnList.Add(Conformance.SearchModifierCodeId.NotIn.ToString());          
      SearchParamType.Reference => new SearchModifierCodeId[] {
                                                                SearchModifierCodeId.Missing,
                                                                SearchModifierCodeId.Type,
                                                              },
      SearchParamType.Composite => new SearchModifierCodeId[] { },
      SearchParamType.Quantity => new SearchModifierCodeId[] { SearchModifierCodeId.Missing },
      SearchParamType.Uri => new SearchModifierCodeId[] {
                                                          SearchModifierCodeId.Missing,
                                                          SearchModifierCodeId.Below,
                                                          SearchModifierCodeId.Above,
                                                          SearchModifierCodeId.Contains,
                                                          SearchModifierCodeId.Exact
                                                        },
      SearchParamType.Special => new SearchModifierCodeId[] { SearchModifierCodeId.Missing },
      _ => throw new System.ComponentModel.InvalidEnumArgumentException(searchParamType.ToString(), (int)searchParamType, typeof(SearchParamType)),
    };
  }
}

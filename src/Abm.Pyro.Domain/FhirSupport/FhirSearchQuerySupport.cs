using System.ComponentModel;
using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Domain.FhirSupport;

public static class FhirSearchQuerySupport
{
    public static SearchComparatorId[] GetPrefixListForSearchType(
        SearchParamType searchParamType)
    {
        return searchParamType switch
        {
            SearchParamType.Number =>
            [
                SearchComparatorId.Ne,
                SearchComparatorId.Eq,
                SearchComparatorId.Gt,
                SearchComparatorId.Ge,
                SearchComparatorId.Lt,
                SearchComparatorId.Le
            ],
            SearchParamType.Date =>
            [
                SearchComparatorId.Ne,
                SearchComparatorId.Eq,
                SearchComparatorId.Gt,
                SearchComparatorId.Ge,
                SearchComparatorId.Lt,
                SearchComparatorId.Le
            ],
            SearchParamType.String => [], //Any search parameter that's value is a string will not have prefixes
            SearchParamType.Token => [], //Any search parameter that's value is a string will not have prefixes
            SearchParamType.Reference => [], //Any search parameter that's value is a string will not have prefixes
            SearchParamType.Composite => [], //Any search parameter that's value is a string will not have prefixes
            SearchParamType.Quantity =>
            [
                SearchComparatorId.Ne,
                SearchComparatorId.Eq,
                SearchComparatorId.Gt,
                SearchComparatorId.Ge,
                SearchComparatorId.Lt,
                SearchComparatorId.Le
            ],
            SearchParamType.Uri => [], //Any search parameter that's value is a string will not have prefixes
            SearchParamType.Special => [],
            _ => throw new InvalidEnumArgumentException(searchParamType.GetCode(), (int)searchParamType,
                typeof(SearchParamType)),
        };
    }

    public static SearchModifierCodeId[] GetModifiersForSearchType(
        SearchParamType searchParamType)
    {
        return searchParamType switch
        {
            SearchParamType.Number => [SearchModifierCodeId.Missing],
            SearchParamType.Date => [SearchModifierCodeId.Missing],
            SearchParamType.String =>
            [
                SearchModifierCodeId.Missing,
                SearchModifierCodeId.Contains,
                SearchModifierCodeId.Exact
            ],
            SearchParamType.Token => new[]
            {
                SearchModifierCodeId.Missing,
                SearchModifierCodeId.Not,
            },
            //The modifiers below are supported in the spec for token but not 
            //implemented by this server as yet
            //ReturnList.Add(Conformance.SearchModifierCodeId.Text.ToString());
            //ReturnList.Add(Conformance.SearchModifierCodeId.In.ToString());
            //ReturnList.Add(Conformance.SearchModifierCodeId.Below.ToString());
            //ReturnList.Add(Conformance.SearchModifierCodeId.Above.ToString());
            //ReturnList.Add(Conformance.SearchModifierCodeId.In.ToString());
            //ReturnList.Add(Conformance.SearchModifierCodeId.NotIn.ToString());          
            SearchParamType.Reference =>
            [
                SearchModifierCodeId.Missing,
                SearchModifierCodeId.Type
            ],
            SearchParamType.Composite => [],
            SearchParamType.Quantity => [SearchModifierCodeId.Missing],
            SearchParamType.Uri =>
            [
                SearchModifierCodeId.Missing,
                SearchModifierCodeId.Below,
                SearchModifierCodeId.Above,
                SearchModifierCodeId.Contains,
                SearchModifierCodeId.Exact
            ],
            SearchParamType.Special => [SearchModifierCodeId.Missing],
            _ => throw new InvalidEnumArgumentException(searchParamType.ToString(), (int)searchParamType,
                typeof(SearchParamType)),
        };
    }
}
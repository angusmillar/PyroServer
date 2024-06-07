using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.SearchQueryEntity;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.FhirQuery;
using SearchParamType = Abm.Pyro.Domain.Enums.SearchParamType;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Domain.SearchQuery;

public class SearchQueryFactory(
  IFhirUriFactory fhirUriFactory,
  IFhirResourceTypeSupport fhirResourceTypeSupport,
  IFhirDateTimeFactory fhirDateTimeFactory,
  ISearchParameterCache searchParameterCache)
  : ISearchQueryFactory
{
  public async Task<IList<SearchQueryBase>> Create(FhirResourceTypeId resourceTypeContext, SearchParameterProjection searchParameter, KeyValuePair<string, StringValues> parameter, bool isChainedReference = false)
  {
    var result = new List<SearchQueryBase>();
    string parameterName = parameter.Key.Trim();
    foreach (string? parameterValue in parameter.Value)
    {
      string parameterValueString = parameterValue ?? string.Empty;
      string rawValue;
      if (isChainedReference)
        rawValue = $"{parameterName}{FhirQuery.FhirQuery.TermChainDelimiter}";
      else
        rawValue = $"{parameterName}={parameterValue ?? string.Empty}";

      SearchQueryBase searchQueryBase = InitializeSearchQueryEntity(searchParameter, resourceTypeContext, isChainedReference, rawValue);
      result.Add(searchQueryBase);

      searchQueryBase.ParseModifier(parameterName, fhirResourceTypeSupport);

      if (searchQueryBase.Modifier == SearchModifierCodeId.Type && !isChainedReference)
      {
        if (searchQueryBase.TypeModifierResource is null)
        {
          throw new NullReferenceException(nameof(searchQueryBase.TypeModifierResource));
        }
        searchQueryBase.ParseValue($"{searchQueryBase.TypeModifierResource.GetCode()}/{parameterValue}");
      }
      else if (searchQueryBase.SearchParameter.Type == SearchParamType.Composite)
      {
        await LoadCompositeSubSearchParameters(resourceTypeContext, searchParameter, parameterValueString, rawValue, searchQueryBase);
      }
      else
      {
        searchQueryBase.ParseValue(parameterValueString);
      }
    }
    return result;
  }

  private async Task LoadCompositeSubSearchParameters(FhirResourceTypeId resourceTypeContext, SearchParameterProjection searchParameter, string parameterValue, string rawValue, SearchQueryBase searchQueryBase)
  {
    if (searchQueryBase is SearchQueryComposite searchQueryComposite)
    {
      List<SearchQueryBase> searchParameterBaseList = new List<SearchQueryBase>();
      IEnumerable<SearchParameterProjection> compositeSearchParameterList = await searchParameterCache.GetListByResourceType(resourceTypeContext);
      IEnumerable<SearchParameterProjection> searchParameterProjections = compositeSearchParameterList as SearchParameterProjection[] ?? compositeSearchParameterList.ToArray();

      foreach (SearchParameterStoreComponent component in searchQueryComposite.SearchParameter.ComponentList) //Should this be ordered by sentinel?
      {
        SearchParameterProjection? compositeSearchParameter = searchParameterProjections.SingleOrDefault(x => x.Url.Equals(component.Definition));
        if (compositeSearchParameter is not null)
        {
          SearchQueryBase compositeSubSearchQueryBase = InitializeSearchQueryEntity(compositeSearchParameter, resourceTypeContext, false, rawValue);
          searchParameterBaseList.Add(compositeSubSearchQueryBase);
        }
        else
        {
          //This should not ever happen, but have message in case it does. We should never have a Composite
          //search parameter loaded like this as on load it is checked, but you never know!
          string message =
            $"Unable to locate one of the SearchParameters referenced in a Composite SearchParameter type. " +
            //$"The Composite SearchParameter Url was: {SearchQueryComposite.Url} for the resource type '{ResourceContext.GetCode()}'. " +
            $"This SearchParameter references another SearchParameter with the Canonical Url of {component.Definition}. " +
            $"This SearchParameter can not be located in the FHIR Server. This is most likely a server error that will require investigation to resolve";
          searchQueryComposite.InvalidMessage = message;
          searchQueryComposite.IsValid = false;
          break;
        }
      }
      searchQueryComposite.ParseCompositeValue(searchParameterBaseList, parameterValue);
    }
    else
    {
      throw new InvalidCastException($"Unable to cast a {nameof(searchQueryBase)} to {nameof(SearchQueryComposite)} when the {nameof(searchQueryBase)}.{nameof(searchQueryBase.SearchParameter.Type)} = {searchQueryBase.SearchParameter.Type.GetCode()}");
    }
  }

  private SearchQueryBase InitializeSearchQueryEntity(SearchParameterProjection searchParameter, FhirResourceTypeId ResourceContext, bool IsChained, string RawValue)
  {
    return searchParameter.Type switch {
      SearchParamType.Number => new SearchQueryNumber(searchParameter, ResourceContext, RawValue),
      SearchParamType.Date => new SearchQueryDateTime(searchParameter, ResourceContext, RawValue, fhirDateTimeFactory),
      SearchParamType.String => new SearchQueryString(searchParameter, ResourceContext, RawValue),
      SearchParamType.Token => new SearchQueryToken(searchParameter, ResourceContext, RawValue),
      SearchParamType.Reference => new SearchQueryReference(searchParameter, ResourceContext, fhirUriFactory, RawValue, IsChained),
      SearchParamType.Composite => new SearchQueryComposite(searchParameter, ResourceContext, RawValue),
      SearchParamType.Quantity => new SearchQueryQuantity(searchParameter, ResourceContext, RawValue),
      SearchParamType.Uri => new SearchQueryUri(searchParameter, ResourceContext, RawValue),
      SearchParamType.Special => new SearchQueryNumber(searchParameter, ResourceContext, RawValue),
      _ => throw new System.ComponentModel.InvalidEnumArgumentException(searchParameter.Type.ToString(), (int)searchParameter.Type, typeof(Enums.SearchParamType)),
    };
  }

}

using Abm.Pyro.Application.SearchQueryChain;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirQuery;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.SearchQueryEntity;
using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Application.SearchQuery;

public class SearchQueryService(
  ISearchQueryFactory searchQueryFactory,
  IFhirResourceTypeSupport fhirResourceTypeSupport,
  IChainQueryProcessingService chainQueryProcessingService,
  ISearchParameterCache searchParameterCache,
  IFhirQuery fhirQuery)
  : ISearchQueryService
{
  private FhirResourceTypeId ResourceContext { get; set; }
  private SearchQueryServiceOutcome? Outcome { get; set; }

  public async Task<SearchQueryServiceOutcome> Process(FhirResourceTypeId resourceTypeContext, string? queryString)
  {
    var parseHttpQuery = queryString.ParseHttpQuery(); 
    ResourceContext = resourceTypeContext;
    fhirQuery.Parse(parseHttpQuery);
    
    Outcome = new SearchQueryServiceOutcome(ResourceContext, fhirQuery)
    {
      PageRequested = fhirQuery.Page,
      OriginalSearchQuery = parseHttpQuery
    };

    //Parse Include and RevInclude parameters
    await ProcessIncludeSearchParameters(fhirQuery.Include);
    await ProcessHasList(fhirQuery.Has);

    foreach (var parameter in fhirQuery.ParametersDictionary)
    {
      //We will just ignore an empty parameter such as this last '&' URL?family=Smith&given=John&
      if (parameter.Key + parameter.Value != string.Empty)
      {
        if (parameter.Key.Contains(FhirQuery.TermChainDelimiter))
        {
          ChainQueryProcessingOutcome chainQueryProcessingOutcome = await chainQueryProcessingService.Process(this.ResourceContext, parameter);
          chainQueryProcessingOutcome.SearchQueryList.ForEach(x => Outcome.SearchQueryList.Add(x));
          chainQueryProcessingOutcome.InvalidSearchQueryList.ForEach(x => Outcome.InvalidSearchQueryList.Add(x));
          chainQueryProcessingOutcome.UnsupportedSearchQueryList.ForEach(x => Outcome.UnsupportedSearchQueryList.Add(x));
        }
        else
        {
          await NormalSearchProcessing(parameter);
        }
      }
    }
    
    return Outcome;
  }

  private async Task ProcessHasList(IEnumerable<FhirQuery.HasParameter> hasList)
  {
    foreach (var has in hasList)
    {
      SearchQueryHas? result = await ProcessHas(has, has.RawHasParameter);
      if (result is not null)
      {
        Outcome!.HasList.Add(result);
      }
    }
  }

  private async Task<SearchQueryHas?> ProcessHas(FhirQuery.HasParameter has, string rawHasParameter)
  {
    var result = new SearchQueryHas();
    string message;

    FhirResourceTypeId? targetResourceForSearchQuery = fhirResourceTypeSupport.TryGetResourceType(has.TargetResourceForSearchQuery);
    if (targetResourceForSearchQuery.HasValue)
    {
      result.TargetResourceForSearchQuery = targetResourceForSearchQuery.Value;
    }
    else
    {
      Outcome!.InvalidSearchQueryList.Add(new InvalidQueryParameter(rawHasParameter, $"The resource type name of: {has.TargetResourceForSearchQuery} in a {FhirQuery.TermHas} parameter could not be resolved to a resource type supported by this server. "));
      return null;
    }
    
    IEnumerable<SearchParameterProjection> baseResourceSearchParameterList = await searchParameterCache.GetListByResourceType(FhirResourceTypeId.Resource);
    IEnumerable<SearchParameterProjection> searchParameterList = baseResourceSearchParameterList.Concat(await searchParameterCache.GetListByResourceType(result.TargetResourceForSearchQuery));
    SearchParameterProjection? backReferenceSearchParameter = searchParameterList.SingleOrDefault(x => x.Code == has.BackReferenceSearchParameterName);
    if (backReferenceSearchParameter is not null && backReferenceSearchParameter.Type == SearchParamType.Reference)
    {
      result.BackReferenceSearchParameter = backReferenceSearchParameter;
    }
    else
    {
      if (backReferenceSearchParameter is null)
      {
        message = $"The reference search parameter back to the target resource type of: {has.BackReferenceSearchParameterName} is not a supported search parameter for the resource type {this.ResourceContext.GetCode()} within this server.";
        Outcome!.InvalidSearchQueryList.Add(new InvalidQueryParameter(rawHasParameter, message));
        return null;
      }
    }

    if (has.ChildHasParameter is not null)
    {
      result.ChildSearchQueryHas = await ProcessHas(has.ChildHasParameter, rawHasParameter);
      return result;
    }


    if (has.SearchQuery.HasValue)
    {
      searchParameterList = await searchParameterCache.GetListByResourceType(result.TargetResourceForSearchQuery);
      SearchParameterProjection? searchParameter = searchParameterList.SingleOrDefault(x => x.Code == has.SearchQuery.Value.Key);
      if (searchParameter is not null)
      {
        IList<SearchQueryBase> searchQueryBaseList = await searchQueryFactory.Create(this.ResourceContext, searchParameter, has.SearchQuery.Value);
        if (searchQueryBaseList.Count == 1)
        {
          if (searchQueryBaseList[0].IsValid)
          {
            result.SearchQuery = searchQueryBaseList[0];
            return result;
          }
          message = $"Error parsing the search parameter found at the end of a {FhirQuery.TermHas} query. The search parameter name was : {has.SearchQuery.Value.Key} with the value of {has.SearchQuery.Value.Value}. " +
                    $"Additional information: {searchQueryBaseList[0].InvalidMessage}";
          Outcome!.InvalidSearchQueryList.Add(new InvalidQueryParameter(rawHasParameter, message));
          return null;
        }
        throw new ApplicationException($"The {FhirQuery.TermHas} parameter seems to end with more then one search parameter, this should not be possible.");
      }
      message = $"The {FhirQuery.TermHas} query finish with a search parameter: {has.SearchQuery.Value.Key} for the resource type of: {result.TargetResourceForSearchQuery.GetCode()}. " +
                $"However, the search parameter: {has.SearchQuery.Value.Key} is not a supported search parameter for this resource type in by this server. ";
      Outcome!.InvalidSearchQueryList.Add(new InvalidQueryParameter(rawHasParameter, message));
      return null;
    }

    message = $"The {FhirQuery.TermHas} query does not finish with a search parameter and value.";
    Outcome!.InvalidSearchQueryList.Add(new InvalidQueryParameter(rawHasParameter, message));
    return null;
  }

  private async Task NormalSearchProcessing(KeyValuePair<string, StringValues> parameter)
  {
    IEnumerable<SearchParameterProjection> baseResourceSearchParameterList = await searchParameterCache.GetListByResourceType(FhirResourceTypeId.Resource);
    IEnumerable<SearchParameterProjection> searchParameterList = baseResourceSearchParameterList.Concat(await searchParameterCache.GetListByResourceType(ResourceContext));
    
    //Remove modifiers
    var searchParameterCode = parameter.Key.Split(FhirQuery.TermSearchModifierDelimiter)[0].Trim();
    SearchParameterProjection? searchParameter = searchParameterList.SingleOrDefault(x => x.Code.Equals(searchParameterCode, StringComparison.Ordinal));
    if (searchParameter is null)
    {
      string message = $"The search query parameter: {parameter.Key} is not supported by this server for the resource type: {ResourceContext.ToString()}, the whole parameter was : {parameter.Key}={parameter.Value.ToString()}";
      Outcome!.UnsupportedSearchQueryList.Add(new InvalidQueryParameter(parameter.Key, parameter.Value.ToString(), message));
    }
    else
    {
      IList<SearchQueryBase> searchQueryBaseList = await searchQueryFactory.Create(this.ResourceContext, searchParameter, parameter);
      foreach (SearchQueryBase searchQueryBase in searchQueryBaseList)
      {
        if (searchQueryBase.IsValid)
        {
          Outcome!.SearchQueryList.Add(searchQueryBase);
        }
        else
        {
          Outcome!.InvalidSearchQueryList.Add(new InvalidQueryParameter(searchQueryBase.RawValue, searchQueryBase.InvalidMessage ?? String.Empty));
        }
      }
    }
  }

  private async Task ProcessIncludeSearchParameters(IList<FhirQuery.IncludeParameter>? includeList)
  {
    if (includeList is null)
    {
      return;
    }
    
    foreach (var include in includeList)
    {
      string rawParameter = $"{IncludeType.Include.GetCode()}";
      if (include.Iterate)
      {
        rawParameter += $":{FhirQuery.TermIncludeIterate}";
      }
      if (include.Recurse)
      {
        rawParameter += $":{FhirQuery.TermIncludeRecurse}";
      }
      rawParameter += $"={include.Value}";
      
      SearchQueryInclude searchParameterInclude = new SearchQueryInclude(include.Type) {
                                                                                         IsIterate = include.Iterate,
                                                                                         IsRecurse = include.Recurse
                                                                                       };

      var valueSplitArray = include.Value.Split(FhirQuery.TermSearchModifierDelimiter);
      string sourceResource = valueSplitArray[0].Trim();
      FhirResourceTypeId? sourceResourceType = fhirResourceTypeSupport.TryGetResourceType(sourceResource);
      if (sourceResourceType.HasValue)
      {
        searchParameterInclude.SourceResourceType = sourceResourceType.Value;
      }
      else
      {
        Outcome!.InvalidSearchQueryList.Add(new InvalidQueryParameter(rawParameter, $"The source resource type of: {sourceResource} for the {include.Type.GetCode()} parameter is not recognized as a FHIR resource type by this server. "));
        break;
      }

      if (valueSplitArray.Count() > 2)
      {
        string targetResourceTypeString = valueSplitArray[2].Trim();
        //checked we have a something if we don't then that is fine just a syntax error of the callers part 
        //i.e _includes=Patient:subject:
        if (!string.IsNullOrWhiteSpace(targetResourceTypeString))
        {
          FhirResourceTypeId? targetResourceType = fhirResourceTypeSupport.TryGetResourceType(targetResourceTypeString);
          if (targetResourceType.HasValue)
          {
            searchParameterInclude.SearchParameterTargetResourceType = targetResourceType.Value;
          }
          else
          {
            Outcome!.InvalidSearchQueryList.Add(new InvalidQueryParameter(rawParameter, $"The target resource type of : {targetResourceTypeString} for the {include.Type.GetCode()} parameter is not recognized as a FHIR resource type by this server. "));
            break;
          }
        }
      }

      if (valueSplitArray.Count() > 1)
      {
        string searchTerm = valueSplitArray[1].Trim();
        
        IEnumerable<SearchParameterProjection> baseResourceSearchParameterList = await searchParameterCache.GetListByResourceType(FhirResourceTypeId.Resource);
        IEnumerable<SearchParameterProjection> searchParameterList = baseResourceSearchParameterList.Concat(await searchParameterCache.GetListByResourceType(searchParameterInclude.SourceResourceType.Value));
        if (searchTerm == "*")
        {
          if (searchParameterInclude.SearchParameterTargetResourceType is not null)
          {
            searchParameterInclude.SearchParameterList = searchParameterList.Where(x => 
              x.Type == SearchParamType.Reference && 
              x.TargetList.Any(v => 
                v.ResourceType == searchParameterInclude.SearchParameterTargetResourceType.Value))
              .ToList();
          }
          else
          {
            searchParameterInclude.SearchParameterList = searchParameterList.Where(x => 
              x.Type == SearchParamType.Reference).ToList();
          }
        }
        else
        {
          SearchParameterProjection? searchParameter = searchParameterList.SingleOrDefault(x => x.Code == searchTerm);
          if (searchParameter is not null)
          {
            if (searchParameter.Type == SearchParamType.Reference)
            {
              if (searchParameterInclude.SearchParameterTargetResourceType.HasValue)
              {
                if (searchParameter.TargetList.All(x => x.ResourceType != searchParameterInclude.SearchParameterTargetResourceType.Value))
                {
                  string message = $"The target Resource '{searchParameterInclude.SearchParameterTargetResourceType.Value.GetCode()}' of the _includes parameter is not recognized for the source '{searchParameterInclude.SourceResourceType.GetCode()}' Resource's search parameter {searchParameter.Code}.";
                  Outcome!.InvalidSearchQueryList.Add(new InvalidQueryParameter(rawParameter, message));
                  break;
                }

                if (include.Type.Equals(IncludeType.Revinclude) && !ResourceContext.Equals(searchParameterInclude.SearchParameterTargetResourceType.Value))
                {
                  string message = $"The target Resource '{searchParameterInclude.SearchParameterTargetResourceType.Value.GetCode()}' of the _revinclude parameter is not equal to the resource type of '{ResourceContext.GetCode()}' which must be implied target for the _revinclude. Resource's search parameter {searchParameter.Code}.";
                  Outcome!.InvalidSearchQueryList.Add(new InvalidQueryParameter(rawParameter, message));
                  break;
                }
              }

              if (include.Type.Equals(IncludeType.Revinclude))
              {
                searchParameterInclude.SearchParameterTargetResourceType = ResourceContext;  
              }
              
              searchParameterInclude.SearchParameterList = new List<SearchParameterProjection> {
                                                                                                 searchParameter
                                                                                               };
            }
            else
            {
              string message = $"The source Resource '{searchParameterInclude.SourceResourceType.GetCode()}' search parameter '{searchParameter.Code}' of the _includes parameter is not of search parameter of type Reference, found search parameter type of '{searchParameter.Type.GetCode()}'.";
              Outcome!.InvalidSearchQueryList.Add(new InvalidQueryParameter(rawParameter, message));
              break;
            }
          }
          else
          {
            string message = $"The source Resource '{searchParameterInclude.SourceResourceType.GetCode()}' search parameter '{searchTerm}' is not a valid search parameter for the source Resource type.";
            Outcome!.InvalidSearchQueryList.Add(new InvalidQueryParameter(rawParameter, message));
            break;
          }
        }
      }
      Outcome!.IncludeList.Add(searchParameterInclude);
    }
  }
}

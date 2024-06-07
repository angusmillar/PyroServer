using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirQuery;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.SearchQueryEntity;

namespace Abm.Pyro.Application.SearchQueryChain;

public class ChainQueryProcessingService(
  IFhirResourceTypeSupport fhirResourceTypeSupport,
  ISearchQueryFactory searchQueryFactory,
  ISearchParameterCache searchParameterCache)
  : IChainQueryProcessingService
{
  private FhirResourceTypeId ResourceTypeContext;
    private SearchQueryBase? ParentChainSearchParameter;
    private SearchQueryBase? PreviousChainSearchParameter;
    private bool ErrorInSearchParameterProcessing;
    private readonly List<InvalidQueryParameter> InvalidSearchQueryParameterList = new();
    private readonly List<InvalidQueryParameter> UnsupportedSearchQueryParameterList = new();
    private string RawParameter = string.Empty;


    public async Task<ChainQueryProcessingOutcome> Process(FhirResourceTypeId resourceTypeContext, KeyValuePair<string, StringValues> parameter)
    {
      ResourceTypeContext = resourceTypeContext;
      ParentChainSearchParameter = null;
      PreviousChainSearchParameter = null;
      ErrorInSearchParameterProcessing = false;
      InvalidSearchQueryParameterList.Clear();
      UnsupportedSearchQueryParameterList.Clear();
      
      
      var outcome = new ChainQueryProcessingOutcome();
      RawParameter = $"{parameter.Key}={parameter.Value}";
      string[] chainedParameterSplit = parameter.Key.Split(FhirQuery.TermChainDelimiter);

      for (int i = 0; i < chainedParameterSplit.Length; i++)
      {
        //Each segment in the chain is a IsChainedReference except the last segment in the chain which has the value.
        bool isChainedReference = i != chainedParameterSplit.Length - 1;
        string parameterNameWithModifier = parameter.Key.Split(FhirQuery.TermChainDelimiter)[i];
        StringValues parameterValue = String.Empty;
        //There is no valid Value for a chained reference parameter unless it is the last in a series 
        //of chains, so don't set it unless this is the last parameter in the whole chain.         
        if (i == chainedParameterSplit.Length - 1)
        {
          parameterValue = parameter.Value;
        }
          

        var singleChainedParameter = new KeyValuePair<string, StringValues>(parameterNameWithModifier, parameterValue);

        string parameterName = String.Empty;
        string parameterModifierTypedResource = String.Empty;

        //Check for and deal with modifiers e.g 'Patient' in the example: subject:Patient.family=millar
        if (parameterNameWithModifier.Contains(FhirQuery.TermSearchModifierDelimiter))
        {
          string[] parameterModifierSplit = parameterNameWithModifier.Split(FhirQuery.TermSearchModifierDelimiter);
          parameterName = parameterModifierSplit[0].Trim();

          if (isChainedReference && parameterModifierSplit.Length > 1)
          {
            FhirResourceTypeId? modifierResourceType = fhirResourceTypeSupport.TryGetResourceType(parameterModifierSplit[1].Trim());
            if (modifierResourceType.HasValue)
            {
              parameterModifierTypedResource = parameterModifierSplit[1].Trim();
            }
            else
            {
              ErrorInSearchParameterProcessing = true;
              //If the Parent is ok then we can assume that any error further down the chain is an invalid search term rather than an unsupported term
              //as it is clear that this is a FHIR search term and not some other search parameter forgen to FHIR
              if (ParentChainSearchParameter is not null)
              {
                InvalidSearchQueryParameterList.Add(new InvalidQueryParameter(RawParameter, $"The resource type modifier: {parameterModifierSplit[1].Trim()} within the chained search query of {RawParameter} is not a known FHIR resource within this server. "));
              }
              else
              {
                //Here we are only looking up the ParameterName to check weather this should be an unsupported parameter or an invalid parameter.
                //If we know the ParameterName then it is invalid whereas if we don't then it is unsupported and both are not known.
                var tempSearchParameterList = await GetSearchParameterListForResourceType(ResourceTypeContext);
                var tempSearchParameter = tempSearchParameterList.SingleOrDefault(x => x.Code == parameterName); 
                if (tempSearchParameter is null)
                {
                  string message = $"Both the search parameter name: {parameterName} for the resource type: {ResourceTypeContext.GetCode()} and its resource type modifier: {parameterModifierSplit[1].Trim()} within the chained search query of {RawParameter} are unsupported. ";
                  UnsupportedSearchQueryParameterList.Add(new InvalidQueryParameter(RawParameter, message));
                }
                else
                {
                  InvalidSearchQueryParameterList.Add(new InvalidQueryParameter(RawParameter, $"The resource type modifier: {parameterModifierSplit[1].Trim()} within the chained search query of {RawParameter} is not a known resource type. "));
                }
              }                            
              break;
            }
          }
        }
        else
        {
          parameterName = parameterNameWithModifier;
        }

        var searchParameter = await GetSearchParameter(parameterName);

        //We have no resolved a SearchParameter so we parse the value if it is the end of the chain, see IsChainedReference 
        //or we are only parsing as a chain segment with no end value
        if (searchParameter != null)
        {          
          //If this is the last parameter in the chain then treat is as not a chain, otherwise treat as a chain         
          await SetChain(searchParameter, singleChainedParameter, isChainedReference);
        }
        else
        {
          ErrorInSearchParameterProcessing = true;
          if (InvalidSearchQueryParameterList.Count == 0 && UnsupportedSearchQueryParameterList.Count == 0)
          {
            throw new ApplicationException("Internal Server Error: When processing a chain search query we failed to resolve a search parameter for the query string however their are " +
              $"no items found in either the {nameof(ChainQueryProcessingService.InvalidSearchQueryParameterList)} or the {nameof(ChainQueryProcessingService.UnsupportedSearchQueryParameterList)}. This is an error in its self.");
          }          
          break;
        }
      }

      //End of Chain loop
      if (!ErrorInSearchParameterProcessing)
      {
        if (ParentChainSearchParameter is not null)
        {
          outcome!.SearchQueryList.Add(ParentChainSearchParameter);
          return outcome;
        }
        else
        {
          throw new NullReferenceException(nameof(PreviousChainSearchParameter));
        }
      }
      else
      {
        InvalidSearchQueryParameterList.ForEach(x => outcome!.InvalidSearchQueryList.Add(x));
        UnsupportedSearchQueryParameterList.ForEach(x => outcome!.UnsupportedSearchQueryList.Add(x));
        return outcome;
      }

    }

    private async Task SetChain(SearchParameterProjection searchParameter, KeyValuePair<string, StringValues> singleChainedParameter,  bool isChainedReference)
    {
      IList<SearchQueryBase> searchQueryBaseList = await searchQueryFactory.Create(ResourceTypeContext, searchParameter, singleChainedParameter, isChainedReference);

      foreach (SearchQueryBase searchQueryBase in searchQueryBaseList)
      {
        if (searchQueryBase.IsValid)
        {
          if (searchQueryBase.CloneDeep() is SearchQueryBase searchQueryBaseClone)
          {
            if (ParentChainSearchParameter is null)
            {
              ParentChainSearchParameter = searchQueryBaseClone;
            }
            else
            {
              if (ParentChainSearchParameter.ChainedSearchParameter is null)
              {
                ParentChainSearchParameter.ChainedSearchParameter = searchQueryBaseClone;
              }

              if (PreviousChainSearchParameter is not null)
              {
                PreviousChainSearchParameter.ChainedSearchParameter = searchQueryBaseClone;
              }
              else
              {
                throw new NullReferenceException(nameof(ChainQueryProcessingService.PreviousChainSearchParameter));
              }
            }

            PreviousChainSearchParameter = searchQueryBaseClone;
            if (isChainedReference)
              PreviousChainSearchParameter.Modifier = SearchModifierCodeId.Type;
          }
          else
          {
            throw new InvalidCastException($"Internal Server Error: Unable to cast cloned SearchQueryBase to ISearchQueryBase");
          }
        }
        else
        {
          string message = $"Failed to parse the value of the chain search query. Additional information: {searchQueryBase.InvalidMessage}";
          ErrorInSearchParameterProcessing = true;
          InvalidSearchQueryParameterList.Add(new InvalidQueryParameter(RawParameter, message));
          break;
        }
      }
    }

    private async Task<SearchParameterProjection?> GetSearchParameter(string parameterName)
    {
      SearchParameterProjection? searchParameter;
      //Here we go through a series of ways to locate the SearchParameter for each segment of the chain query
      if (PreviousChainSearchParameter is null)
      {
        var searchParameterList = await GetSearchParameterListForResourceType(ResourceTypeContext);
        //If there is no previous then we look through the search parameter for the root resource type stored in this.ResourceContext
        searchParameter = searchParameterList.SingleOrDefault(x => x.Code == parameterName);
        if (searchParameter is null)
        {
          ErrorInSearchParameterProcessing = true;
          UnsupportedSearchQueryParameterList.Add(
            new InvalidQueryParameter(this.RawParameter, 
                                            $"The resource search parameter name of: {parameterName} within the chained search query of: {RawParameter} is not a known search parameter for the resource: " +
                                            $"{ResourceTypeContext}. "));
          return null;
        }
        else
        {
          return searchParameter;
        }
      }
      else
      {
        //Here we are using the PreviousChainSearchParameter's TypeModifierResource as the context to find the search parameter
        if (!PreviousChainSearchParameter.TypeModifierResource.HasValue)
        {
          //If there is no TypeModifierResource on the previous then we look at how many it supports and if only one we can use that.
          if (PreviousChainSearchParameter.SearchParameter.TargetList.Count == 1)
          {
            PreviousChainSearchParameter.TypeModifierResource = PreviousChainSearchParameter.SearchParameter.TargetList.ToArray()[0].ResourceType;
            ResourceTypeContext = PreviousChainSearchParameter.SearchParameter.TargetList.ToArray()[0].ResourceType;
            
            var searchParametersListForTarget = await GetSearchParameterListForResourceType(PreviousChainSearchParameter.SearchParameter.TargetList.ToArray()[0].ResourceType);
            SearchParameterProjection? searchParameterForTarget = searchParametersListForTarget.SingleOrDefault(x => x.Code == parameterName);
            if (searchParameterForTarget is null)
            {
              string message = $"Unable to locate the search parameter named: {parameterName} for the resource type: {PreviousChainSearchParameter.TypeModifierResource} within the chain search query of: {RawParameter}";
              ErrorInSearchParameterProcessing = true;
              InvalidSearchQueryParameterList.Add(new InvalidQueryParameter(RawParameter, message));
              return null;
            }
            else
            {
              searchParameter = searchParameterForTarget;
              return searchParameter;
            }
          }
          else
          {
            //If more than one then we search for the given search parameter name among all  resource types supported for the PreviousChainSearchParameter
            var multiChainedSearchParameter = new Dictionary<FhirResourceTypeId, SearchParameterProjection>();
            foreach (var targetResourceType in PreviousChainSearchParameter.SearchParameter.TargetList)
            {
              var searchParametersListForTarget = await GetSearchParameterListForResourceType(targetResourceType.ResourceType);
              SearchParameterProjection? searchParameterForTarget = searchParametersListForTarget.SingleOrDefault(x => x.Code == parameterName);
              if (searchParameterForTarget is not null)
              {
                multiChainedSearchParameter.Add(targetResourceType.ResourceType, searchParameterForTarget);
              }
            }
            if (multiChainedSearchParameter.Count == 1)
            {
              //If this resolves to only one found then we use it
              PreviousChainSearchParameter.TypeModifierResource = multiChainedSearchParameter.First().Key;
              
              ResourceTypeContext = multiChainedSearchParameter.First().Key;
              
              searchParameter = multiChainedSearchParameter.First().Value;
              return searchParameter;
            }
            else
            {
              if (multiChainedSearchParameter.Count > 1)
              {
                //We still have many to choose from so it cannot be resolved. The user need to specify the ResourceType with the Type modifier on the search parameter query e.g subject:Patient.family  
                string refResources = String.Empty;
                foreach (var dicItem in multiChainedSearchParameter)
                {
                  refResources += ", " + dicItem.Key.GetCode();
                }
                string message = String.Empty;
                message = $"The chained search parameter '{RawParameter}' is ambiguous. ";
                message += $"The search parameter '{PreviousChainSearchParameter.SearchParameter.Code}' can reference any of the following resource types ({refResources.TrimStart(',').Trim()}). ";
                message += $"Please use a Type Modifier to explicitly state the desired search parameter target. For example: {PreviousChainSearchParameter.SearchParameter.Code}:{multiChainedSearchParameter.First().Key.GetCode()}.{multiChainedSearchParameter.First().Value.Code} ";
                message += $"If the '{multiChainedSearchParameter.First().Key.GetCode()}' resource type was the intended target reference for the search parameter '{PreviousChainSearchParameter.SearchParameter.Code}'.";
                ErrorInSearchParameterProcessing = true;
                InvalidSearchQueryParameterList.Add(new InvalidQueryParameter(RawParameter, message));
                return null;
              }
              else
              {
                //We have found zero matches for this search parameter name from the previous allowed resource types, so the search parameter name is possibly wrong.
                string targetResourceTypes = string.Empty;
                foreach (var targetResourceType in PreviousChainSearchParameter.SearchParameter.TargetList)
                {
                  targetResourceTypes += ", " + targetResourceType.ResourceType.GetCode();
                }

                string message = $"The chained search parameter '{RawParameter}' is unresolvable. ";
                message += $"Additional information: ";
                message += $"The search parameter: {parameterName} should be a search parameter for any of the following resource types ({targetResourceTypes.TrimStart(',').Trim()}) as resolved from the previous link in the chain: {PreviousChainSearchParameter.SearchParameter.Code}. ";
                message += $"To correct this you must specify a search parameter here that is supported by those resource types. ";
                message += $"Please review your chained search query and specifically the use of the search parameter: {parameterName}'";
                ErrorInSearchParameterProcessing = true;
                InvalidSearchQueryParameterList.Add(new InvalidQueryParameter(this.RawParameter, message));
                return null;
              }                           
            }
          }
        }
        else if (CheckModifierTypeResourceValidForSearchParameter(PreviousChainSearchParameter.TypeModifierResource.Value, PreviousChainSearchParameter.SearchParameter.TargetList))
        {
          //PreviousChainSearchParameter.TypeModifierResource = PreviousChainSearchParameter.TypeModifierResource;
          //Double check the final Type modifier resource resolved is valid for the previous search parameter, the user could have got it wrong in the query.
          FhirResourceTypeId resourceTypeTest = PreviousChainSearchParameter.TypeModifierResource.Value;
          ResourceTypeContext = resourceTypeTest;
          
          var tempSearchParameterList = await GetSearchParameterListForResourceType(resourceTypeTest);
          searchParameter = tempSearchParameterList.SingleOrDefault(x => x.Code == parameterName);
          if (searchParameter is not null)
          {
            return searchParameter;
          }
          else
          {
            string resourceName = resourceTypeTest.GetCode();
            string message = $"The chained search query part: {parameterName} is not a supported search parameter name for the resource type: {resourceName}. ";
            message += $"Additional information: ";
            message += $"This search parameter was a chained search parameter. The part that was not recognized was: {parameterName}.";
            ErrorInSearchParameterProcessing = true;
            InvalidSearchQueryParameterList.Add(new InvalidQueryParameter(this.RawParameter, message));
            return null;
          }          
        }
        else
        {
          //The modifier target resource provided is not valid for the previous reference, e.g subject:DiagnosticReport.family=millar                        
          string resourceName = this.ResourceTypeContext.GetCode();
          string message = $"The search parameter '{parameterName}' is not supported by this server for the resource type '{resourceName}'. ";
          message += $"Additional information: ";
          message += $"This search parameter was a chained search parameter. The part that was not recognized was '{PreviousChainSearchParameter.SearchParameter.Code}.{parameterName}', The search parameter modifier given '{PreviousChainSearchParameter.TypeModifierResource}' is not valid for the search parameter {PreviousChainSearchParameter.SearchParameter.Code}. ";
          ErrorInSearchParameterProcessing = true;
          InvalidSearchQueryParameterList.Add(new InvalidQueryParameter(this.RawParameter, message));
          return null;
        }
      }
    }

    private bool CheckModifierTypeResourceValidForSearchParameter(FhirResourceTypeId modifierTypeResource, ICollection<SearchParameterStoreResourceTypeTarget> targetResourceTypeList)
    {
      return targetResourceTypeList.Any(x => x.ResourceType == modifierTypeResource);
    }

    private async Task<IEnumerable<SearchParameterProjection>> GetSearchParameterListForResourceType(FhirResourceTypeId resourceType)
    {
      IEnumerable<SearchParameterProjection> baseResourceSearchParameterList = await searchParameterCache.GetListByResourceType(FhirResourceTypeId.Resource);
      return baseResourceSearchParameterList.Concat(await searchParameterCache.GetListByResourceType(resourceType));
    }
}


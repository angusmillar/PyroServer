using System.Linq.Expressions;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;

namespace Abm.Pyro.Repository.Predicates
{
  public class IndexReferencePredicateFactory(IServiceBaseUrlCache serviceBaseUrlCache, IFhirResourceTypeSupport fhirResourceTypeSupport) : IIndexReferencePredicateFactory
  {
    public async Task<List<Expression<Func<IndexReference, bool>>>> ReferenceIndex(SearchQueryReference searchQueryReference)
    {
      ServiceBaseUrl primaryServiceBaseUrl = await serviceBaseUrlCache.GetRequiredPrimaryAsync();
      if (!primaryServiceBaseUrl.ServiceBaseUrlId.HasValue)
      {
        throw new NullReferenceException(nameof(primaryServiceBaseUrl.ServiceBaseUrlId));
      }
      if (!searchQueryReference.SearchParameter.SearchParameterStoreId.HasValue)
      {
        throw new NullReferenceException(nameof(searchQueryReference.SearchParameter.SearchParameterStoreId));
      }
      
      var resultList = new List<Expression<Func<IndexReference, bool>>>();
      //Improved Query when searching for ResourceIds for the same ResourceType and search parameter yet different ResourceIds.
      //It creates a SQL 'IN' cause instead of many 'OR' statements and should be more efficient.        
      //Heavily used in chain searching where we traverse many References. 
      //The 'Type' modifier is already resolved when the search parameter is parsed, so the SearchValue.FhirRequestUri.ResourceName is the correct Resource name at this stage
      if (searchQueryReference.ValueList.Count > 1 && searchQueryReference.ValueList.TrueForAll(x =>
                                                                                                  !x.IsMissing &&
                                                                                                  x.FhirUri!.IsRelativeToServer &&
                                                                                                  x.FhirUri.ResourceName == searchQueryReference.ValueList[0].FhirUri!.ResourceName &&
                                                                                                  string.IsNullOrWhiteSpace(x.FhirUri.VersionId)))
      {
        

        var quickIndexReferencePredicate = LinqKit.PredicateBuilder.New<IndexReference>();
        string[] referenceFhirIdArray = searchQueryReference.ValueList.Select(x => x.FhirUri!.ResourceId).ToArray();
        quickIndexReferencePredicate = quickIndexReferencePredicate.And(IsSearchParameterId(searchQueryReference.SearchParameter.SearchParameterStoreId.Value));
        quickIndexReferencePredicate = quickIndexReferencePredicate.And(EqualTo_ByKey_Many_ResourceIds(primaryServiceBaseUrl.ServiceBaseUrlId.Value,
                                                                                                       searchQueryReference.ValueList[0].FhirUri!.ResourceName,
                                                                                                       referenceFhirIdArray,
                                                                                                       searchQueryReference.ValueList[0].FhirUri!.VersionId));
        resultList.Add(quickIndexReferencePredicate);
        return resultList;
      }

      foreach (SearchQueryReferenceValue referenceValue in searchQueryReference.ValueList)
      {
        var indexReferencePredicate = LinqKit.PredicateBuilder.New<IndexReference>();
        indexReferencePredicate = indexReferencePredicate.And(IsSearchParameterId(searchQueryReference.SearchParameter.SearchParameterStoreId.Value));

        if (!searchQueryReference.Modifier.HasValue)
        {
          if (referenceValue.FhirUri is not null)
          {
            if (referenceValue.FhirUri.IsRelativeToServer)
            {

              indexReferencePredicate = indexReferencePredicate.And(EqualTo_ByKey(primaryServiceBaseUrl.ServiceBaseUrlId.Value, referenceValue.FhirUri.ResourceName, referenceValue.FhirUri.ResourceId, referenceValue.FhirUri.VersionId));
              resultList.Add(indexReferencePredicate);
              //ResourceStorePredicate = ResourceStorePredicate.Or(AnyIndex(IndexReferencePredicate));
            }
            else
            {
              indexReferencePredicate = indexReferencePredicate.And(EqualTo_ByUrlString(referenceValue.FhirUri.PrimaryServiceRootRemote!.OriginalString, referenceValue.FhirUri.ResourceName, referenceValue.FhirUri.ResourceId, referenceValue.FhirUri.VersionId));
              resultList.Add(indexReferencePredicate);
            }
          }
          else
          {
            throw new ArgumentNullException(nameof(referenceValue.FhirUri));
          }
        }
        else
        {
          var arrayOfSupportedModifiers = FhirSearchQuerySupport.GetModifiersForSearchType(searchQueryReference.SearchParameter.Type);
          if (arrayOfSupportedModifiers.Contains(searchQueryReference.Modifier.Value))
          {
            switch (searchQueryReference.Modifier.Value)
            {
              case SearchModifierCodeId.Missing:
                indexReferencePredicate = indexReferencePredicate.And(IsNotSearchParameterId(searchQueryReference.SearchParameter.SearchParameterStoreId.Value));
                resultList.Add(indexReferencePredicate);
                break;
              default:
                throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryReference.Modifier.Value.GetCode()} has been added to the supported list for {searchQueryReference.SearchParameter.Type.GetCode()} search parameter queries and yet no database predicate has been provided.");
            }
          }
          else
          {
            throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryReference.Modifier.Value.GetCode()} is not supported for search parameter types of {searchQueryReference.SearchParameter.Type.GetCode()}.");
          }
        }

      }
      return resultList;
    }

    private Expression<Func<IndexReference, bool>> EqualTo_ByKey_Many_ResourceIds(int primaryServiceBaseUrlId, string resourceName, string[] resourceIdArray, string versionId)
    {
      string? nullableVersionId = versionId.NullIfEmptyString();
      FhirResourceTypeId resourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(resourceName);
      return x => x.ServiceBaseUrlId == primaryServiceBaseUrlId && x.ResourceType == resourceType && resourceIdArray.Contains(x.ResourceId) && x.VersionId == nullableVersionId;
    }

    private Expression<Func<IndexReference, bool>> EqualTo_ByKey(int primaryServiceBaseUrlId, string resourceName, string resourceId, string versionId)
    {
      string? nullableVersionId = versionId.NullIfEmptyString();
      FhirResourceTypeId resourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(resourceName);
      return x => x.ServiceBaseUrlId == primaryServiceBaseUrlId && x.ResourceType == resourceType && x.ResourceId == resourceId && x.VersionId == nullableVersionId;
    }

    private Expression<Func<IndexReference, bool>> EqualTo_ByUrlString(string remoteServiceBaseUrl, string resourceName, string resourceId, string versionId)
    {
      string? nullableVersionId = versionId.NullIfEmptyString();
      FhirResourceTypeId resourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(resourceName);
      remoteServiceBaseUrl = remoteServiceBaseUrl.StripHttp();
      return x => x.ServiceBaseUrl != null && x.ServiceBaseUrl.Url == remoteServiceBaseUrl && x.ResourceType == resourceType && x.ResourceId == resourceId && x.VersionId == nullableVersionId;
    }


    private Expression<Func<ResourceStore, bool>> AnyIndex(Expression<Func<IndexReference, bool>> predicate)
    {
      return x => x.IndexReferenceList.Any(predicate.Compile());
    }
    private Expression<Func<ResourceStore, bool>> AnyIndexEquals(Expression<Func<IndexReference, bool>> predicate, bool equals)
    {
      return x => x.IndexReferenceList.Any(predicate.Compile()) == equals;
    }
    private Expression<Func<IndexReference, bool>> IsSearchParameterId(int searchParameterId)
    {
      return x => x.SearchParameterStoreId == searchParameterId;
    }
    private Expression<Func<IndexReference, bool>> IsNotSearchParameterId(int searchParameterId)
    {
      return x => x.SearchParameterStoreId != searchParameterId;
    }
    
  }
}

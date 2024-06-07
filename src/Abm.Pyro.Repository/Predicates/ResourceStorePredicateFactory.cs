using System.Linq.Expressions;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;

namespace Abm.Pyro.Repository.Predicates;

public class ResourceStorePredicateFactory(
  IIndexReferencePredicateFactory indexReferencePredicateFactory,
  IIndexStringPredicateFactory indexStringPredicateFactory,
  IIndexTokenPredicateFactory indexTokenPredicateFactory,
  IIndexNumberPredicateFactory indexNumberPredicateFactory,
  IIndexDateTimePredicateFactory indexDateTimePredicateFactory,
  IIndexQuantityPredicateFactory indexQuantityPredicateFactory,
  IIndexUriPredicateFactory indexUriPredicateFactory,
  IIndexCompositePredicateFactory indexCompositePredicateFactory)
  : IResourceStorePredicateFactory
{
  public Expression<Func<ResourceStore, bool>> CurrentMainResource(FhirResourceTypeId resourceType)
  {
    return x => x.ResourceType == resourceType && x.IsCurrent && !x.IsDeleted;
  }

  public async Task<List<Expression<Func<IndexReference, bool>>>> ReferenceIndex(SearchQueryBase searchQueryBase)
  {
    if (searchQueryBase is SearchQueryReference searchQueryReference)
    {
      return await indexReferencePredicateFactory.ReferenceIndex(searchQueryReference);
    }
    
    throw new InvalidCastException($"Unable to cast a {nameof(searchQueryBase)} of type {searchQueryBase.GetType().Name} to a {nameof(SearchQueryReference)}");
  }
  
  public List<Expression<Func<IndexString, bool>>> StringIndex(SearchQueryBase searchQueryBase)
  {
    if (searchQueryBase is SearchQueryString searchQueryString)
    {
      return indexStringPredicateFactory.StringIndex(searchQueryString);
    }

    throw new InvalidCastException($"Unable to cast a {nameof(SearchQueryBase)} of type {searchQueryBase.GetType().Name} to a {nameof(SearchQueryString)}");
  }

  public List<Expression<Func<IndexToken, bool>>> TokenIndex(SearchQueryBase searchQueryBase)
  {
    if (searchQueryBase is SearchQueryToken searchQueryToken)
    {
      return indexTokenPredicateFactory.TokenIndex(searchQueryToken);
    }
    
    throw new InvalidCastException($"Unable to cast a {nameof(SearchQueryBase)} of type {searchQueryBase.GetType().Name} to a {nameof(SearchQueryToken)}");
  }

  public List<Expression<Func<IndexQuantity, bool>>> NumberIndex(SearchQueryBase searchQueryBase)
  {
    if (searchQueryBase is SearchQueryNumber searchQueryNumber)
    {
      return indexNumberPredicateFactory.NumberIndex(searchQueryNumber);
    }
    
    throw new InvalidCastException($"Unable to cast a {nameof(SearchQueryBase)} of type {searchQueryBase.GetType().Name} to a {nameof(SearchQueryNumber)}");
  }

  public List<Expression<Func<IndexDateTime, bool>>> DateTimeIndex(SearchQueryBase searchQueryBase)
  {
    if (searchQueryBase is SearchQueryDateTime searchQueryDateTime)
    {
      return indexDateTimePredicateFactory.DateTimeIndex(searchQueryDateTime);
    }
    
    throw new InvalidCastException($"Unable to cast a {nameof(SearchQueryBase)} of type {searchQueryBase.GetType().Name} to a {nameof(SearchQueryDateTime)}");
  }

  public List<Expression<Func<IndexQuantity, bool>>> QuantityIndex(SearchQueryBase searchQueryBase)
  {
    if (searchQueryBase is SearchQueryQuantity searchQueryQuantity)
    {
      return indexQuantityPredicateFactory.QuantityIndex(searchQueryQuantity);
    }
    
    throw new InvalidCastException($"Unable to cast a {nameof(SearchQueryBase)} of type {searchQueryBase.GetType().Name} to a {nameof(SearchQueryQuantity)}");
  }
  
  public List<Expression<Func<IndexUri, bool>>> UriIndex(SearchQueryBase searchQueryBase)
  {
    if (searchQueryBase is SearchQueryUri searchQueryUri)
    {
      return indexUriPredicateFactory.UriIndex(searchQueryUri);
    }
    
    throw new InvalidCastException($"Unable to cast a {nameof(SearchQueryBase)} of type {searchQueryBase.GetType().Name} to a {nameof(SearchQueryUri)}");
  }
  
  public async Task<Expression<Func<ResourceStore, bool>>> CompositeIndex(ISearchPredicateFactory searchPredicateFactory, SearchQueryBase searchQueryBase)
  {
    if (searchQueryBase is SearchQueryComposite searchQueryComposite)
    {
      return await indexCompositePredicateFactory.CompositeIndex(searchPredicateFactory, searchQueryComposite);
    }
    
    throw new InvalidCastException($"Unable to cast a {nameof(searchQueryBase)} of type {searchQueryBase.GetType().Name} to a {nameof(searchQueryComposite)}");
  }

}

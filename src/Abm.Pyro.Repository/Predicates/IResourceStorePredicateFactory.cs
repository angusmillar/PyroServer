using System.Linq.Expressions;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IResourceStorePredicateFactory
{
  Expression<Func<ResourceStore, bool>> CurrentMainResource(FhirResourceTypeId resourceType);
  Task<List<Expression<Func<IndexReference, bool>>>> ReferenceIndex(SearchQueryBase searchQueryBase);
  List<Expression<Func<IndexString, bool>>> StringIndex(SearchQueryBase searchQueryBase);
  List<Expression<Func<IndexToken, bool>>> TokenIndex(SearchQueryBase searchQueryBase);
  List<Expression<Func<IndexQuantity, bool>>> NumberIndex(SearchQueryBase searchQueryBase);
  List<Expression<Func<IndexDateTime, bool>>> DateTimeIndex(SearchQueryBase searchQueryBase);
  List<Expression<Func<IndexQuantity, bool>>> QuantityIndex(SearchQueryBase searchQueryBase);
  List<Expression<Func<IndexUri, bool>>> UriIndex(SearchQueryBase searchQueryBase);
  Task<Expression<Func<ResourceStore, bool>>> CompositeIndex(ISearchPredicateFactory searchPredicateFactory, SearchQueryBase searchQueryBase);
}

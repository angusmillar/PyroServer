using System.Linq.Expressions;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IResourceStorePredicateFactory
{
  Expression<Func<ResourceStore, bool>> CurrentMainResource(FhirResourceTypeId resourceType);
  Task<Expression<Func<ResourceStore, bool>>> ReferenceIndex(SearchQueryBase searchQueryBase);
  Expression<Func<ResourceStore, bool>> StringIndex(SearchQueryBase searchQueryBase);
  Expression<Func<ResourceStore, bool>> TokenIndex(SearchQueryBase searchQueryBase);
  Expression<Func<ResourceStore, bool>> NumberIndex(SearchQueryBase searchQueryBase);
  Expression<Func<ResourceStore, bool>> DateTimeIndex(SearchQueryBase searchQueryBase);
  Expression<Func<ResourceStore, bool>> QuantityIndex(SearchQueryBase searchQueryBase);
  Expression<Func<ResourceStore, bool>> UriIndex(SearchQueryBase searchQueryBase);
  List<Expression<Func<IndexPosition, bool>>> PositionIndex(SearchQueryBase searchQueryBase);
  Expression<Func<ResourceStore, bool>> PositionIndexMissing(SearchQueryBase searchQueryBase);
  Task<Expression<Func<ResourceStore, bool>>> CompositeIndex(ISearchPredicateFactory searchPredicateFactory, SearchQueryBase searchQueryBase);
}

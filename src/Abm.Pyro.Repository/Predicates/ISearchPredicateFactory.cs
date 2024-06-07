using LinqKit;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface ISearchPredicateFactory
{
  ExpressionStarter<ResourceStore> CurrentMainResourcePredicate(FhirResourceTypeId resourceType);
  Task<ExpressionStarter<ResourceStore>> GetResourceStoreIndexPredicate(IEnumerable<SearchQueryBase> searchQueryList);
}

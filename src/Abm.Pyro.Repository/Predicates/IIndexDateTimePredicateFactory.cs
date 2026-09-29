using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IIndexDateTimePredicateFactory
{
  Expression<Func<ResourceStore, bool>> DateTimeIndex(SearchQueryDateTime searchQueryDateTime);
}

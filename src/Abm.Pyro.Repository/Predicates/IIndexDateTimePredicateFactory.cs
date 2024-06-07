using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IIndexDateTimePredicateFactory
{
  List<Expression<Func<IndexDateTime, bool>>> DateTimeIndex(SearchQueryDateTime searchQueryDateTime);
}

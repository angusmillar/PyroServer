using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.SearchQueryEntity;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Domain.SearchQuery;

public interface ISearchQueryFactory
{
  Task<IList<SearchQueryBase>> Create(FhirResourceTypeId resourceTypeContext, SearchParameterProjection searchParameter, KeyValuePair<string, StringValues> parameter, bool isChainedReference = false);
}

using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Projection;

namespace Abm.Pyro.Domain.Cache;

public interface ISearchParameterCache
{
  Task<IEnumerable<SearchParameterProjection>> GetListByResourceType(FhirResourceTypeId resourceType);
  Task Remove(FhirResourceTypeId resourceType);
}

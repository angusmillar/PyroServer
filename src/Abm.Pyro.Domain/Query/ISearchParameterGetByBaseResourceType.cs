using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Projection;

namespace Abm.Pyro.Domain.Query;

public interface ISearchParameterGetByBaseResourceType
{
  Task<IEnumerable<SearchParameterProjection>> Get(FhirResourceTypeId resourceType);
}

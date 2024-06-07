using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Projection;

namespace Abm.Pyro.Domain.Query;

public interface ISearchParameterMetaDataGetByBaseResourceType
{
  Task<IEnumerable<SearchParameterMetaDataProjection>> Get(FhirResourceTypeId resourceType);
}

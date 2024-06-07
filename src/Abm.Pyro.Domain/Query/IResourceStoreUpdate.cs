using Abm.Pyro.Domain.Projection;

namespace Abm.Pyro.Domain.Query;

public interface IResourceStoreUpdate
{
  public Task Update(ResourceStoreUpdateProjection resourceStoreUpdateProjection, bool deleteFhirIndexes);
}

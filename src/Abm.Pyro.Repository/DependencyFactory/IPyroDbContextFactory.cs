using Abm.Pyro.Domain.Configuration;

namespace Abm.Pyro.Repository.DependencyFactory;

public interface IPyroDbContextFactory
{
    public PyroDbContext Get(Tenant tenant);
}
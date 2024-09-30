namespace Abm.Pyro.Repository.DependencyFactory;

public interface IPyroDbContextFactory
{
    public PyroDbContext Get();
}
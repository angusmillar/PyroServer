namespace Abm.Pyro.Domain.Cache;

public interface IActiveSubscriptionCache
{
    Task<ICollection<ActiveSubscription>> GetList();
    Task RefreshCache();
}
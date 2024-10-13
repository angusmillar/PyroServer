namespace Abm.Pyro.Application.Cache;

public interface IActiveSubscriptionCache
{
    Task<ICollection<ActiveSubscription>> GetList();
    Task RefreshCache();
}
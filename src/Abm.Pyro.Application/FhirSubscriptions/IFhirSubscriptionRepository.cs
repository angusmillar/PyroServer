using Abm.Pyro.Application.Cache;

namespace Abm.Pyro.Application.FhirSubscriptions;

public interface IFhirSubscriptionRepository
{
    Task<ICollection<ActiveSubscription>> GetActiveSubscriptionList(CancellationToken cancellationToken);
}
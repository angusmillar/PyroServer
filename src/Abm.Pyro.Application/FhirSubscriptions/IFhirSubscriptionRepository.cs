using Abm.Pyro.Application.Cache;
using Abm.Pyro.Domain.Cache;

namespace Abm.Pyro.Application.FhirSubscriptions;

public interface IFhirSubscriptionRepository
{
    Task<ICollection<ActiveSubscription>> GetActiveSubscriptionList(CancellationToken cancellationToken);
}
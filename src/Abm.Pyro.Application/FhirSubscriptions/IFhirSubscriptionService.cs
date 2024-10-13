using Abm.Pyro.Application.Cache;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.FhirSubscriptions;

public interface IFhirSubscriptionService
{
    Task<AcceptSubscriptionOutcome> CanSubscriptionBeAccepted(Subscription subscription);
    
}
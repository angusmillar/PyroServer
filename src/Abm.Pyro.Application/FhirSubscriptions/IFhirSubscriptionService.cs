using Abm.Pyro.Domain.FhirSubscriptions;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Application.FhirSubscriptions;

public interface IFhirSubscriptionService
{
    Task<AcceptSubscriptionOutcome> CanSubscriptionBeAccepted(Subscription subscription);
    
}
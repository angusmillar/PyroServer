
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.Extensions;

public static class BundleExtensions
{
    
    public static IEnumerable<Bundle.EntryComponent> GetByRequestMethodType(this IEnumerable<Bundle.EntryComponent>? entryComponentList, Bundle.HTTPVerb httpVerb)
    {
        if (entryComponentList is null || !entryComponentList.Any())
        {
            return Enumerable.Empty<Bundle.EntryComponent>();
        }
        
        return entryComponentList.Where(x => x.Request?.Method is not null && x.Request?.Method == httpVerb);
    }
}
using Abm.Pyro.Application.MetaDataService;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Caching.Distributed;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.TenantService;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.Cache;

public class MetaDataCache(
  IDistributedCache distributedCache,
  IMetaDataService metaDataService,
  IFhirSerializationSupport fhirSerializationSupport,
  IFhirDeSerializationSupport fhirDeSerializationSupport,
  ITenantService tenantService)
  : BaseDistributedCache<string>(distributedCache, tenantService), IMetaDataCache
{
  
  public async Task<CapabilityStatement> GetCapabilityStatement()
  {
    string primaryKey = GetPrimaryKey();
    string? capabilityStatementJson = await TryGetValueAsync(primaryKey);
    if (capabilityStatementJson is null)
    {
      CapabilityStatement capabilityStatementResource = await metaDataService.GetCapabilityStatement();
      capabilityStatementJson = fhirSerializationSupport.ToJson(capabilityStatementResource, SummaryType.False, pretty: false);
      await SetAsync(primaryKey, capabilityStatementJson);
      
    }
    Resource? resource = fhirDeSerializationSupport.ToResource(capabilityStatementJson);
    ArgumentNullException.ThrowIfNull(resource);
    if (resource is CapabilityStatement capabilityStatement)
    {
      return capabilityStatement;
    }

    throw new InvalidCastException(nameof(capabilityStatement));
  }

  public async Task Remove()
  {
    await RemoveAsync(GetPrimaryKey());
  }

  private async Task SetAsync(string key, string capabilityStatementJson)
  {
    await SetAsync(key, capabilityStatementJson, GetDistributedCacheEntryOptions());
  }

  private static string GetPrimaryKey()
  {
    return "METADATA";
  }

  private static DistributedCacheEntryOptions GetDistributedCacheEntryOptions()
  {
    return new DistributedCacheEntryOptions() {
                                                SlidingExpiration = TimeSpan.FromHours(12),           //Will expire the entry if it has not been accessed in a set amount of time.
                                                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) //Will expire the entry after a set amount of time.                                                                
                                              };
  }
}

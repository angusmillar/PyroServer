namespace Abm.Pyro.Application.Cache;

public static class CacheSupport
{
    public static string GetCacheKey(string tenantCode, string key)
    {
        return $"{tenantCode}:{key}";
    }
    
}
namespace Abm.Pyro.Domain.Configuration;

public class KnownProxiesSettings
{
    public const string SectionName = "KnownProxies";
    
    public List<string> ProxyIpAddressOrHostName { get; set; } = new List<string>();
}
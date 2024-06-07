#pragma warning disable CS8618
namespace Abm.Pyro.Domain.Model;

  public class ServiceBaseUrl : DbBase
  {
    private ServiceBaseUrl() : base() { }
    
    public ServiceBaseUrl(int? serviceBaseUrlId, string url, bool isPrimary)
    {
      IsPrimary = isPrimary;
      ServiceBaseUrlId = serviceBaseUrlId;
      Url = url;
    }
    public int? ServiceBaseUrlId { get; set; }
    public string Url { get; set; }
    public bool IsPrimary { get; set; }
  }


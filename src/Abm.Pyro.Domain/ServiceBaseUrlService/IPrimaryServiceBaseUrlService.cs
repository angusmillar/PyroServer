namespace Abm.Pyro.Domain.ServiceBaseUrlService;

public interface IPrimaryServiceBaseUrlService
{
    Task<Domain.Model.ServiceBaseUrl> GetServiceBaseUrlAsync();
    Task<string> GetUrlAsync();
    Task<Uri> GetUriAsync();
    string GetUrlString();
    Uri GetUri();
}
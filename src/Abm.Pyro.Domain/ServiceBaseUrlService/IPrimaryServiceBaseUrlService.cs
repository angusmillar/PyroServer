namespace Abm.Pyro.Domain.ServiceBaseUrlService;

public interface IPrimaryServiceBaseUrlService
{
    Task<Uri> GetUriAsync();
    Uri GetUri();
}
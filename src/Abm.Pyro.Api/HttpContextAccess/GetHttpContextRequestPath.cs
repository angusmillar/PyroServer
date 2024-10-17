using Abm.Pyro.Domain.TenantService;

namespace Abm.Pyro.Api.HttpContextAccess;

public class GetHttpContextRequestPath(IHttpContextAccessor httpContextAccessor): IGetHttpContextRequestPath
{
    public string Get()
    {
        return httpContextAccessor.HttpContext?.Request.Path.Value ?? throw new NullReferenceException(nameof(httpContextAccessor));
    }
}
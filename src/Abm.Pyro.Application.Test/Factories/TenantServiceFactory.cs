using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.TenantService;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Abm.Pyro.Application.Test.Factories;

public class TenantServiceFactory
{
    public static ITenantService GetTest()
    {
        var logger = new Mock<ILogger<TenantService>>();
        var tenantSettingsMock = new Mock<IOptions<TenantSettings>>();
        var getHttpContextRequestPathMock = new Mock<IGetHttpContextRequestPath>();
        TenantService tenantService =  new TenantService(logger.Object, tenantSettingsMock.Object, getHttpContextRequestPathMock.Object);

        var tenant = new Domain.Configuration.Tenant()
        {
            Code = "pyro-test",
            DisplayName = "Pyro Test Tenant",
            SqlConnectionStringCode = "pyro-test",
            UrlCode = "pyro-test"
        };
        
        tenantService.SetScopedTenant(tenant);

        return tenantService;
    }
}
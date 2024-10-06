using Abm.Pyro.Application.Tenant;
using Abm.Pyro.Domain.Configuration;
using Microsoft.AspNetCore.Http;
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
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        TenantService tenantService =  new TenantService(logger.Object, tenantSettingsMock.Object, httpContextAccessorMock.Object);

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
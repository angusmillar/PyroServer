using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Microsoft.EntityFrameworkCore;

namespace Abm.Pyro.Repository.Query;

public class ServiceConfigurationGetCurrentByType(PyroDbContext context) : IServiceConfigurationGetCurrentByType
{
    public async Task<ServiceSetting> Get(ServiceSettingTypeId typeId)
    {
        return await context.ServiceSetting.SingleAsync(x => x.ServiceSettingTypeId == typeId && x.IsCurrent == true);
        
    }
}
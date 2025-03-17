using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Microsoft.EntityFrameworkCore;

namespace Abm.Pyro.Repository.Query;

public class ServiceConfigurationGetHistory(PyroDbContext context) : IServiceConfigurationGetHistory
{
    public async Task<List<ServiceSetting>> History(ServiceSettingTypeId typeId)
    {
        return await context.ServiceSetting
            .Where(x => x.ServiceSettingTypeId == typeId)
            .OrderBy(x => x.LastUpdatedUtc)
            .ToListAsync();
        
    }
}
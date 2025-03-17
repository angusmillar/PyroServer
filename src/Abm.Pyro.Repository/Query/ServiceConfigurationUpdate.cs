using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;

namespace Abm.Pyro.Repository.Query;

public class ServiceConfigurationUpdate(PyroDbContext context) : IServiceConfigurationUpdate
{
    public async Task Update(ServiceSetting serviceSetting)
    {
        context.ServiceSetting.Update(serviceSetting);
        await context.SaveChangesAsync();
    }
}
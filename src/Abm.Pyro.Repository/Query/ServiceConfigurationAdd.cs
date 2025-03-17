using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;

namespace Abm.Pyro.Repository.Query;

public class ServiceConfigurationAdd(PyroDbContext context) : IServiceConfigurationAdd
{
    public async Task Add(ServiceSetting serviceSetting)
    {
        context.ServiceSetting.Add(serviceSetting);
        await context.SaveChangesAsync();
    }
}
using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IServiceConfigurationAdd
{
    public Task Add(ServiceSetting serviceSetting);
}
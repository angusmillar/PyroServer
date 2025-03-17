using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IServiceConfigurationUpdate
{
    public Task Update(ServiceSetting serviceSetting);
}
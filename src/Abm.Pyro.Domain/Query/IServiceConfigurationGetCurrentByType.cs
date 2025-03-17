using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IServiceConfigurationGetCurrentByType
{
    public Task<ServiceSetting> Get(ServiceSettingTypeId typeId);
}
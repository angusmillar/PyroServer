using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IServiceConfigurationGetHistory
{
    public Task<List<ServiceSetting>> History(ServiceSettingTypeId typeId);
}
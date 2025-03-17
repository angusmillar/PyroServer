using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.ServiceSettings;

namespace Abm.Pyro.Domain.Cache;

public interface IServiceSettingsCache
{
    Task<FhirValidationSettings> GetFhirValidationSettings();
    Task Remove(ServiceSettingTypeId serviceSettingType);
}
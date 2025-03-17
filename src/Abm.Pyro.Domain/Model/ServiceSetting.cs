#pragma warning disable CS8618
using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Domain.Model;

public class ServiceSetting : DbBase
{
    private ServiceSetting() : base() { }
    public ServiceSetting(int? serviceSettingId,
        int versionId,
        bool isCurrent,
        ServiceSettingTypeId typeId,
        string json,
        DateTime lastUpdatedUtc)
    {
        ServiceSettingId = serviceSettingId;
        VersionId = versionId;
        IsCurrent = isCurrent;
        ServiceSettingTypeId = typeId;
        Json = json;
        LastUpdatedUtc = lastUpdatedUtc;
    }

    public int? ServiceSettingId { get; set; }
    public int VersionId { get; set; }
    public bool IsCurrent { get; set; }
    public ServiceSettingTypeId ServiceSettingTypeId { get; set; }
    public string Json { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
    
}
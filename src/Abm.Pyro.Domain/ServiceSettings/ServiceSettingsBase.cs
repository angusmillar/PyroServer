namespace Abm.Pyro.Domain.ServiceSettings;

public abstract class ServiceSettingsBase
{
    protected ServiceSettingsBase(
        string versionId,
        DateTime lastUpdated)
    {
        VersionId = versionId;
        LastUpdated = lastUpdated;
    }

    public string VersionId { get; set; }
    public DateTime LastUpdated { get; set; } 
}
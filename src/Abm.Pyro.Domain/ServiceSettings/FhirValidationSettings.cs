using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.FhirSupport;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.ServiceSettings;

/// <summary>
/// This class is  later serialised to JSON and stored with in the ServiceSettings database entity
/// </summary>
public class FhirValidationSettings : ServiceSettingsBase
{
    public FhirValidationSettings(
        string versionId,
        Uri profilePackageServiceUrl,
        Uri terminologyServiceUrl,
        bool validateOnCreate,
        bool validateOnUpdate,
        DateTime lastUpdated) 
        : base( versionId, lastUpdated)
    {
        VersionId = versionId;
        ProfilePackageServiceUrl = profilePackageServiceUrl;
        TerminologyServiceUrl = terminologyServiceUrl;
        ValidateOnCreate = validateOnCreate;
        ValidateOnUpdate = validateOnUpdate;
        LastUpdated = lastUpdated;
    }
    
    public Uri ProfilePackageServiceUrl { get; set; } 
    public Uri TerminologyServiceUrl { get; set; } 
    public bool ValidateOnCreate { get; set; } 
    public bool ValidateOnUpdate { get; set; }
    
    public static List<ServiceSetting> SeedDefaultSettings()
    {
        var dateTime = new DateTime(2025, 03, 01, 0, 0, 0, DateTimeKind.Utc);
        return new List<ServiceSetting>()
        {
            new ServiceSetting(
                serviceSettingId: 1,
                versionId: 1,
                isCurrent: true,
                typeId: ServiceSettingTypeId.FhirValidation,
                lastUpdatedUtc: dateTime,
                // NOTE: fixed literal, NOT JsonSerializer.Serialize(...). EF Core HasData seed values must be
                // deterministic; serializing at model-build time produced platform-dependent property ordering
                // (Windows dev vs Linux CI), which triggered EF Core 9 PendingModelChangesWarning false-positives
                // and broke the CD migrate step. Keep this string in sync with FhirValidationSettings if its shape changes.
                json: "{\"ProfilePackageServiceUrl\":\"https://some-profile-package-service-url.com\",\"TerminologyServiceUrl\":\"https://some-terminology-service-url.com\",\"ValidateOnCreate\":false,\"ValidateOnUpdate\":false,\"VersionId\":\"1\",\"LastUpdated\":\"2025-03-01T00:00:00Z\"}")
        };
    }
} 
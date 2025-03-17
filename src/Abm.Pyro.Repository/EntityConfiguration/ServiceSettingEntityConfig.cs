using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.ServiceSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class ServiceSettingEntityConfig : IEntityTypeConfiguration<ServiceSetting>
{
    public void Configure(EntityTypeBuilder<ServiceSetting> builder)
    {
        // ServiceSetting Entity: Stored different sets of settings/configuration for the whole services
        //Like appsettings.json but stored in the database, cached, and accessible through the admin RestFul API   
        
        builder.HasKey(x => x.ServiceSettingId);
        builder.HasIndex(x => new { Type = x.ServiceSettingTypeId, x.VersionId }).IsUnique();
        builder.HasIndex(x => new { Type = x.ServiceSettingTypeId, x.VersionId, x.IsCurrent }).IsUnique();
        builder.HasIndex(x => new { Type = x.ServiceSettingTypeId, x.IsCurrent,  });
        
        builder.Property(x => x.IsCurrent);
        builder.Property(x => x.VersionId).IsConcurrencyToken();
        builder.Property(x => x.LastUpdatedUtc).HasPrecision(RepositoryModelConstraints.TimestampPrecision);
        builder.Property(x => x.Json);
        builder.Property(x => x.ServiceSettingTypeId);
        
        builder.HasData(FhirValidationSettings.SeedDefaultSettings());
    }
}
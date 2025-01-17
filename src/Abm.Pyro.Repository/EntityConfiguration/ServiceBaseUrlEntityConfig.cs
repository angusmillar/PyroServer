using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class ServiceBaseUrlEntityConfig : IEntityTypeConfiguration<ServiceBaseUrl>
{
    public void Configure(
        EntityTypeBuilder<ServiceBaseUrl> builder)
    {
        // ServiceBaseUrl ---------------------------------------------------------------
        
        builder.HasKey(x => x.ServiceBaseUrlId);
        
        // ReSharper disable once EntityNameCapturedOnly.Local
        ServiceBaseUrl serviceBaseUrlForPropertyNameCaptureOnly;
        builder.HasIndex(x => new { x.Url, x.IsPrimary })
            .IsUnique()
            .HasFilter($"[{nameof(serviceBaseUrlForPropertyNameCaptureOnly.IsPrimary)}] = 1");

        builder.Property(x => x.IsPrimary);
        builder.Property(x => x.Url).UseCollation(RepositoryModelConstraints.CaseSensitive);
    }
}
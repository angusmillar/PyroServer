using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityMapping;

public class ResourceStoreMapping : IEntityTypeConfiguration<ResourceStore>
{
    public void Configure(EntityTypeBuilder<ResourceStore> builder)
    {
        // ResourceStore Entity
        
        builder.HasKey(x => x.ResourceStoreId);
        builder.HasIndex(x => new { x.ResourceId, x.ResourceType, x.VersionId }).IsUnique();

        builder.Property(x => x.ResourceId)
            .UseCollation(RepositoryModelConstraints.CaseSensitive)
            .HasMaxLength(RepositoryModelConstraints.FhirIdMaxLength);
        
        builder.Property(x => x.VersionId)
            .HasMaxLength(RepositoryModelConstraints.FhirIdMaxLength);
        builder.Property(x => x.ResourceType);
        builder.Property(x => x.IsCurrent);
        builder.Property(x => x.IsDeleted);
        builder.Property(x => x.HttpVerb);
        builder.Property(x => x.Json);
        builder.Property(x => x.LastUpdatedUtc).HasPrecision(RepositoryModelConstraints.TimestampPrecision);
        builder.Property(x => x.RowVersion).IsConcurrencyToken();

        builder.HasMany(c => c.IndexStringList);
        builder.HasMany(c => c.IndexReferenceList);
        builder.HasMany(c => c.IndexDateTimeList);
    }
}
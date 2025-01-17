using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class IndexReferenceEntityConfig : IEntityTypeConfiguration<IndexReference>
{
    public void Configure(EntityTypeBuilder<IndexReference> builder)
    {
        // IndexReference ---------------------------------------------------------------      
        
        builder.HasKey(x => x.IndexReferenceId);
        builder.HasIndex(x => x.ResourceId);
        builder.HasIndex(x => x.VersionId);
        builder.HasIndex(x => x.CanonicalVersionId);

        builder.Property(x => x.ResourceId)
            .UseCollation(RepositoryModelConstraints.CaseSensitive)
            .HasMaxLength(RepositoryModelConstraints.FhirIdMaxLength);
        builder.Property(x => x.VersionId)
            .HasMaxLength(RepositoryModelConstraints.FhirIdMaxLength);
        builder.Property(x => x.CanonicalVersionId)
            .HasMaxLength(RepositoryModelConstraints.FhirIdMaxLength);
      
        builder
            .HasOne<ResourceStore>(x => x.ResourceStore)
            .WithMany(x => x.IndexReferenceList)
            .HasForeignKey(x => x.ResourceStoreId)
            .OnDelete(DeleteBehavior.Cascade);
      
        builder
            .HasOne<ServiceBaseUrl>(x => x.ServiceBaseUrl)
            .WithMany()
            .HasForeignKey(x => x.ServiceBaseUrlId)
            .OnDelete(DeleteBehavior.NoAction);
      
        builder
            .HasOne<SearchParameterStore>(x => x.SearchParameterStore)
            .WithMany()
            .HasForeignKey(x => x.SearchParameterStoreId)
            .OnDelete(DeleteBehavior.NoAction);
        
    }
}
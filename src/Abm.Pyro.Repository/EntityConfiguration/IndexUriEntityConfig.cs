using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class IndexUriEntityConfig : IEntityTypeConfiguration<IndexUri>
{
    public void Configure(EntityTypeBuilder<IndexUri> builder)
    {
        // IndexUri ---------------------------------------------------------------      
        builder.HasKey(x => x.IndexUriId);
        builder.HasIndex(x => x.Uri);

        builder.Property(x => x.Uri).HasMaxLength(RepositoryModelConstraints.StringMaxLength);;
      
        builder
            .HasOne<ResourceStore>(x => x.ResourceStore)
            .WithMany(x => x.IndexUriList)
            .HasForeignKey(x => x.ResourceStoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<SearchParameterStore>(x => x.SearchParameterStore)
            .WithMany()
            .HasForeignKey(x => x.SearchParameterStoreId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
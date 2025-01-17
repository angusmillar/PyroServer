using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class IndexStringEntityConfig : IEntityTypeConfiguration<IndexString>
{
    public void Configure(EntityTypeBuilder<IndexString> builder)
    {
        // IndexString ---------------------------------------------------------------      
        builder.HasKey(x => x.IndexStringId);
        builder.HasIndex(x => x.Value);

        builder.Property(x => x.Value);
      
        builder
            .HasOne<ResourceStore>(x => x.ResourceStore)
            .WithMany(x => x.IndexStringList)
            .HasForeignKey(x => x.ResourceStoreId)
            .OnDelete(DeleteBehavior.Cascade);
      
        builder
            .HasOne<SearchParameterStore>(x => x.SearchParameterStore)
            .WithMany()
            .HasForeignKey(x => x.SearchParameterStoreId)
            .OnDelete(DeleteBehavior.NoAction);
        
    }
}
using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class SearchParameterStoreResourceTypeTargetEntityConfig : IEntityTypeConfiguration<SearchParameterStoreResourceTypeTarget>
{
    public void Configure(EntityTypeBuilder<SearchParameterStoreResourceTypeTarget> builder)
    {
        // SearchParameterStoreResourceTypeTarget ---------------------------------------------------------------
        builder.HasKey(x => x.SearchParameterStoreResourceTypeTargetId);
        builder.HasIndex(x => x.SearchParameterStoreId);
      
        builder.Property(x => x.SearchParameterStoreId);
        builder.Property(x => x.ResourceType).HasConversion<int>();
        builder.HasData(Seed.SearchParameterStoreResourceTypeTargetSeed.Get());
        
    }
}
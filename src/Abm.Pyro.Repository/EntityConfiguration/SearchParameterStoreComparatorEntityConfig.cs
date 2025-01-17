using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class SearchParameterStoreComparatorEntityConfig : IEntityTypeConfiguration<SearchParameterStoreComparator>
{
    public void Configure(EntityTypeBuilder<SearchParameterStoreComparator> builder)
    {
        // SearchParameterStoreComparator ---------------------------------------------------------------
        builder.HasKey(x => x.SearchParameterStoreComparatorId);
        builder.HasIndex(x => x.SearchParameterStoreId);
      
        builder.Property(x => x.SearchParameterStoreId);
        builder.Property(x => x.SearchComparatorId).HasConversion<int>();
        builder.HasData(Seed.SearchParameterStoreComparatorSeed.Get());
        
    }
}
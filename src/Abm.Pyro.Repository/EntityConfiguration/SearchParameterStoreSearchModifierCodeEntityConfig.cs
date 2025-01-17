using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class SearchParameterStoreSearchModifierCodeEntityConfig : IEntityTypeConfiguration<SearchParameterStoreSearchModifierCode>
{
    public void Configure(EntityTypeBuilder<SearchParameterStoreSearchModifierCode> builder)
    {
        // SearchParameterStoreSearchModifierCode ---------------------------------------------------------------
        builder.HasKey(x => x.SearchParameterStoreSearchModifierCodeId);
        builder.HasIndex(x => x.SearchParameterStoreId);
      
        builder.Property(x => x.SearchParameterStoreId);
        builder.Property(x => x.SearchModifierCodeId).HasConversion<int>();
        builder.HasData(Seed.SearchParameterStoreSearchModifierCodeSeed.Get());
        
    }
}
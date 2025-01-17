using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class SearchParameterStoreComponentEntityConfig : IEntityTypeConfiguration<SearchParameterStoreComponent>
{
    public void Configure(EntityTypeBuilder<SearchParameterStoreComponent> builder)
    {
        // SearchParameterStoreComponent ---------------------------------------------------------------
        builder.HasKey(x => x.SearchParameterStoreComponentId);
        builder.HasIndex(x => x.SearchParameterStoreId);

        builder.Property(x => x.SearchParameterStoreId);
        builder.Property(x => x.Definition);
        builder.Property(x => x.Expression);
        builder.HasData(Seed.SearchParameterStoreComponentSeed.Get());
    }
}
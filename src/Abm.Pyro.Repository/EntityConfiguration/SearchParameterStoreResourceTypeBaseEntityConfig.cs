using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class
    SearchParameterStoreResourceTypeBaseEntityConfig : IEntityTypeConfiguration<SearchParameterStoreResourceTypeBase>
{
    public void Configure(EntityTypeBuilder<SearchParameterStoreResourceTypeBase> builder)
    {
        // SearchParameterStoreResourceTypeBase ---------------------------------------------------------------
        builder.HasKey(x => x.SearchParameterStoreResourceTypeBaseId);
        builder.HasIndex(x => x.SearchParameterStoreId);

        builder.Property(x => x.SearchParameterStoreId);
        builder.Property(x => x.ResourceType).HasConversion<int>();
        builder.HasData(Seed.SearchParameterStoreResourceTypeBaseSeed.Get());
    }
}
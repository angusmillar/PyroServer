using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class IndexPositionEntityConfig : IEntityTypeConfiguration<IndexPosition>
{
    public void Configure(
        EntityTypeBuilder<IndexPosition> builder)
    {
        // IndexPosition ---------------------------------------------------------------
        builder.HasKey(x => x.IndexPositionId);

        builder.Property(x => x.Position).HasColumnType("geography").IsRequired();

        builder
            .HasOne<ResourceStore>(x => x.ResourceStore)
            .WithMany(x => x.IndexPositionList)
            .HasForeignKey(x => x.ResourceStoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<SearchParameterStore>(x => x.SearchParameterStore)
            .WithMany()
            .HasForeignKey(x => x.SearchParameterStoreId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

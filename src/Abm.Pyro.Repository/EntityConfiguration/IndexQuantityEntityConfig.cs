using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class IndexQuantityEntityConfig : IEntityTypeConfiguration<IndexQuantity>
{
    public void Configure(
        EntityTypeBuilder<IndexQuantity> builder)
    {
        // IndexQuantity ---------------------------------------------------------------      
        builder.HasKey(x => x.IndexQuantityId);
        builder.HasIndex(x => x.Code);
        builder.HasIndex(x => x.System);
        builder.HasIndex(x => x.Quantity);
        builder.HasIndex(x => x.CodeHigh);
        builder.HasIndex(x => x.SystemHigh);
        builder.HasIndex(x => x.QuantityHigh);

        builder.Property(x => x.Code).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);
        builder.Property(x => x.System).HasMaxLength(RepositoryModelConstraints.StringMaxLength);
        builder.Property(x => x.Quantity).HasPrecision(
            RepositoryModelConstraints.QuantityPrecision,
            RepositoryModelConstraints.QuantityScale);
        builder.Property(x => x.Unit).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);
        
        builder.Property(x => x.CodeHigh).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);
        builder.Property(x => x.SystemHigh).HasMaxLength(RepositoryModelConstraints.StringMaxLength);
        builder.Property(x => x.QuantityHigh).HasPrecision(
            RepositoryModelConstraints.QuantityPrecision,
            RepositoryModelConstraints.QuantityScale);
        builder.Property(x => x.UnitHigh).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);
        
        builder
            .HasOne<ResourceStore>(x => x.ResourceStore)
            .WithMany(x => x.IndexQuantityList)
            .HasForeignKey(x => x.ResourceStoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<SearchParameterStore>(x => x.SearchParameterStore)
            .WithMany()
            .HasForeignKey(x => x.SearchParameterStoreId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
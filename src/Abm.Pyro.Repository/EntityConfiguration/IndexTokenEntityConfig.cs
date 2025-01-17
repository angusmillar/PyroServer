using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class IndexTokenEntityConfig : IEntityTypeConfiguration<IndexToken>
{
    public void Configure(EntityTypeBuilder<IndexToken> builder)
    {
        // IndexToken ---------------------------------------------------------------      
        builder.HasKey(x => x.IndexTokenId);
        builder.HasIndex(x => x.System);
        builder.HasIndex(x => x.Code);

        builder.Property(x => x.System).HasMaxLength(RepositoryModelConstraints.StringMaxLength);
        builder.Property(x => x.Code).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);

        builder
            .HasOne<ResourceStore>(x => x.ResourceStore)
            .WithMany(x => x.IndexTokenList)
            .HasForeignKey(x => x.ResourceStoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<SearchParameterStore>(x => x.SearchParameterStore)
            .WithMany()
            .HasForeignKey(x => x.SearchParameterStoreId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
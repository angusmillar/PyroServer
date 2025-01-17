using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class SearchParameterStoreEntityConfig : IEntityTypeConfiguration<SearchParameterStore>
{
    public void Configure(
        EntityTypeBuilder<SearchParameterStore> builder)
    {
        // SearchParameterStore ------------------------------------------------------

        builder.HasKey(x => x.SearchParameterStoreId);
        builder.HasAlternateKey(x => x.ResourceId);
        builder.HasIndex(x => x.VersionId);
        builder.HasIndex(x => x.Code);

        builder.Property(x => x.ResourceId)
            .UseCollation(RepositoryModelConstraints.CaseSensitive)
            .HasMaxLength(RepositoryModelConstraints.FhirIdMaxLength);
        builder.Property(x => x.VersionId).HasMaxLength(RepositoryModelConstraints.FhirIdMaxLength);
        builder.Property(x => x.IsCurrent);
        builder.Property(x => x.IsDeleted);
        builder.Property(x => x.IsIndexed);
        builder.Property(x => x.Name).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Url);
        builder.Property(x => x.Code).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);
        builder.Property(x => x.Type);
        builder.Property(x => x.Expression);
        builder.Property(x => x.MultipleOr);
        builder.Property(x => x.MultipleAnd);
        builder.Property(x => x.Chain);
        builder.Property(x => x.Json);
        builder.Property(x => x.LastUpdated).HasPrecision(RepositoryModelConstraints.TimestampPrecision);
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        
        builder
            .HasMany<SearchParameterStoreComparator>(x => x.ComparatorList)
            .WithOne(x => x.SearchParameterStore)
            .HasForeignKey(x => x.SearchParameterStoreId);

        builder
            .HasMany<SearchParameterStoreResourceTypeBase>(x => x.BaseList)
            .WithOne(x => x.SearchParameterStore)
            .HasForeignKey(x => x.SearchParameterStoreId);

        builder
            .HasMany<SearchParameterStoreResourceTypeTarget>(x => x.TargetList)
            .WithOne(x => x.SearchParameterStore)
            .HasForeignKey(x => x.SearchParameterStoreId);

        builder
            .HasMany<SearchParameterStoreSearchModifierCode>(x => x.ModifierList)
            .WithOne(x => x.SearchParameterStore)
            .HasForeignKey(x => x.SearchParameterStoreId);

        builder
            .HasMany<SearchParameterStoreComponent>(x => x.ComponentList)
            .WithOne(x => x.SearchParameterStore)
            .HasForeignKey(x => x.SearchParameterStoreId);

        builder.HasData(Seed.SearchParameterSeed.Get());
    }
}
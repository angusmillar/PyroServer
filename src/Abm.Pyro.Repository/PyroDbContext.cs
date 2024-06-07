using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Repository.Conversion;

namespace Abm.Pyro.Repository;

  public class PyroDbContext(DbContextOptions<PyroDbContext> options) : DbContext(options)
  {
      public DbSet<ResourceStore> ResourceStore => Set<ResourceStore>();
    public DbSet<IndexString> IndexString => Set<IndexString>();
    public DbSet<IndexReference> IndexReference => Set<IndexReference>();
    public DbSet<SearchParameterStore> SearchParameterStore => Set<SearchParameterStore>();
    public DbSet<ServiceBaseUrl> ServiceBaseUrl => Set<ServiceBaseUrl>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      // EntityTypeConfiguration -----------------------------------------------------------
      modelBuilder.UseCollation(RepositoryModelConstraints.CaseInsensitive);
      modelBuilder.ApplyConfiguration(new ResourceStoreJsonCompressionConversion());
      
      // ResourceStore ---------------------------------------------------------------      
      modelBuilder.Entity<ResourceStore>().HasKey(x => x.ResourceStoreId);
      modelBuilder.Entity<ResourceStore>().HasIndex(x => new { x.ResourceId, x.ResourceType, x.VersionId }).IsUnique();

      modelBuilder.Entity<ResourceStore>().Property(x => x.ResourceId).HasMaxLength(RepositoryModelConstraints.FhirIdMaxLength);
      modelBuilder.Entity<ResourceStore>().Property(x => x.VersionId).HasMaxLength(RepositoryModelConstraints.FhirIdMaxLength);
      modelBuilder.Entity<ResourceStore>().Property(x => x.ResourceType);
      modelBuilder.Entity<ResourceStore>().Property(x => x.IsCurrent);
      modelBuilder.Entity<ResourceStore>().Property(x => x.IsDeleted);
      modelBuilder.Entity<ResourceStore>().Property(x => x.HttpVerb);
      modelBuilder.Entity<ResourceStore>().Property(x => x.Json);
      modelBuilder.Entity<ResourceStore>().Property(x => x.LastUpdatedUtc).HasPrecision(RepositoryModelConstraints.TimestampPrecision);
      modelBuilder.Entity<ResourceStore>().Property(x => x.RowVersion).IsConcurrencyToken();

      modelBuilder.Entity<ResourceStore>().HasMany(c => c.IndexStringList);
      modelBuilder.Entity<ResourceStore>().HasMany(c => c.IndexReferenceList);
      modelBuilder.Entity<ResourceStore>().HasMany(c => c.IndexDateTimeList);
      
      // IndexString ---------------------------------------------------------------      
      modelBuilder.Entity<IndexString>().HasKey(x => x.IndexStringId);
      modelBuilder.Entity<IndexString>().HasIndex(x => x.Value);

      modelBuilder.Entity<IndexString>().Property(x => x.Value);
      
      modelBuilder.Entity<IndexString>()
                  .HasOne<ResourceStore>(x => x.ResourceStore)
                  .WithMany(x => x.IndexStringList)
                  .HasForeignKey(x => x.ResourceStoreId)
                  .OnDelete(DeleteBehavior.Cascade);
      
      modelBuilder.Entity<IndexString>()
                  .HasOne<SearchParameterStore>(x => x.SearchParameterStore)
                  .WithMany()
                  .HasForeignKey(x => x.SearchParameterStoreId)
                  .OnDelete(DeleteBehavior.NoAction);
      
      // IndexReference ---------------------------------------------------------------      
      modelBuilder.Entity<IndexReference>().HasKey(x => x.IndexReferenceId);
      modelBuilder.Entity<IndexReference>().HasIndex(x => x.ResourceId);
      modelBuilder.Entity<IndexReference>().HasIndex(x => x.VersionId);
      modelBuilder.Entity<IndexReference>().HasIndex(x => x.CanonicalVersionId);

      modelBuilder.Entity<IndexReference>().Property(x => x.ResourceId).HasMaxLength(RepositoryModelConstraints.FhirIdMaxLength);
      modelBuilder.Entity<IndexReference>().Property(x => x.VersionId).HasMaxLength(RepositoryModelConstraints.FhirIdMaxLength);
      modelBuilder.Entity<IndexReference>().Property(x => x.CanonicalVersionId).HasMaxLength(RepositoryModelConstraints.FhirIdMaxLength);
      
      modelBuilder.Entity<IndexReference>()
                  .HasOne<ResourceStore>(x => x.ResourceStore)
                  .WithMany(x => x.IndexReferenceList)
                  .HasForeignKey(x => x.ResourceStoreId)
                  .OnDelete(DeleteBehavior.Cascade);
      
      modelBuilder.Entity<IndexReference>()
                  .HasOne<ServiceBaseUrl>(x => x.ServiceBaseUrl)
                  .WithMany()
                  .HasForeignKey(x => x.ServiceBaseUrlId)
                  .OnDelete(DeleteBehavior.NoAction);
      
      modelBuilder.Entity<IndexReference>()
                  .HasOne<SearchParameterStore>(x => x.SearchParameterStore)
                  .WithMany()
                  .HasForeignKey(x => x.SearchParameterStoreId)
                  .OnDelete(DeleteBehavior.NoAction);
      
      // IndexDateTime ---------------------------------------------------------------      
      modelBuilder.Entity<IndexDateTime>().HasKey(x => x.IndexDateTimeId);
      modelBuilder.Entity<IndexDateTime>().HasIndex(x => x.LowUtc);
      modelBuilder.Entity<IndexDateTime>().HasIndex(x => x.HighUtc);
      
      modelBuilder.Entity<IndexDateTime>().Property(x => x.LowUtc);
      modelBuilder.Entity<IndexDateTime>().Property(x => x.HighUtc);
      
      modelBuilder.Entity<IndexDateTime>()
                  .HasOne<ResourceStore>(x => x.ResourceStore)
                  .WithMany(x => x.IndexDateTimeList)
                  .HasForeignKey(x => x.ResourceStoreId)
                  .OnDelete(DeleteBehavior.Cascade);
      
      modelBuilder.Entity<IndexDateTime>()
                  .HasOne<SearchParameterStore>(x => x.SearchParameterStore)
                  .WithMany()
                  .HasForeignKey(x => x.SearchParameterStoreId)
                  .OnDelete(DeleteBehavior.NoAction);
      
      // IndexQuantity ---------------------------------------------------------------      
      modelBuilder.Entity<IndexQuantity>().HasKey(x => x.IndexQuantityId);
      modelBuilder.Entity<IndexQuantity>().HasIndex(x => x.Code);
      modelBuilder.Entity<IndexQuantity>().HasIndex(x => x.System);
      modelBuilder.Entity<IndexQuantity>().HasIndex(x => x.Quantity);
      modelBuilder.Entity<IndexQuantity>().HasIndex(x => x.CodeHigh);
      modelBuilder.Entity<IndexQuantity>().HasIndex(x => x.SystemHigh);
      modelBuilder.Entity<IndexQuantity>().HasIndex(x => x.QuantityHigh);
      
      modelBuilder.Entity<IndexQuantity>().Property(x => x.Code).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);
      modelBuilder.Entity<IndexQuantity>().Property(x => x.System).HasMaxLength(RepositoryModelConstraints.StringMaxLength);
      modelBuilder.Entity<IndexQuantity>().Property(x => x.Quantity).HasPrecision(RepositoryModelConstraints.QuantityPrecision, RepositoryModelConstraints.QuantityScale);
      modelBuilder.Entity<IndexQuantity>().Property(x => x.Unit).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);;
      modelBuilder.Entity<IndexQuantity>().Property(x => x.CodeHigh).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);;
      modelBuilder.Entity<IndexQuantity>().Property(x => x.SystemHigh).HasMaxLength(RepositoryModelConstraints.StringMaxLength);
      modelBuilder.Entity<IndexQuantity>().Property(x => x.QuantityHigh).HasPrecision(RepositoryModelConstraints.QuantityPrecision, RepositoryModelConstraints.QuantityScale);
      modelBuilder.Entity<IndexQuantity>().Property(x => x.UnitHigh).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);;
      
      modelBuilder.Entity<IndexQuantity>()
                  .HasOne<ResourceStore>(x => x.ResourceStore)
                  .WithMany(x => x.IndexQuantityList)
                  .HasForeignKey(x => x.ResourceStoreId)
                  .OnDelete(DeleteBehavior.Cascade);
      
      modelBuilder.Entity<IndexQuantity>()
                  .HasOne<SearchParameterStore>(x => x.SearchParameterStore)
                  .WithMany()
                  .HasForeignKey(x => x.SearchParameterStoreId)
                  .OnDelete(DeleteBehavior.NoAction);
      
      // IndexToken ---------------------------------------------------------------      
      modelBuilder.Entity<IndexToken>().HasKey(x => x.IndexTokenId);
      modelBuilder.Entity<IndexToken>().HasIndex(x => x.System);
      modelBuilder.Entity<IndexToken>().HasIndex(x => x.Code);

      modelBuilder.Entity<IndexToken>().Property(x => x.System).HasMaxLength(RepositoryModelConstraints.StringMaxLength);;
      modelBuilder.Entity<IndexToken>().Property(x => x.Code).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);;
      
      modelBuilder.Entity<IndexToken>()
                  .HasOne<ResourceStore>(x => x.ResourceStore)
                  .WithMany(x => x.IndexTokenList)
                  .HasForeignKey(x => x.ResourceStoreId)
                  .OnDelete(DeleteBehavior.Cascade);
      
      modelBuilder.Entity<IndexToken>()
                  .HasOne<SearchParameterStore>(x => x.SearchParameterStore)
                  .WithMany()
                  .HasForeignKey(x => x.SearchParameterStoreId)
                  .OnDelete(DeleteBehavior.NoAction);
      
      // IndexUri ---------------------------------------------------------------      
      modelBuilder.Entity<IndexUri>().HasKey(x => x.IndexUriId);
      modelBuilder.Entity<IndexUri>().HasIndex(x => x.Uri);

      modelBuilder.Entity<IndexUri>().Property(x => x.Uri).HasMaxLength(RepositoryModelConstraints.StringMaxLength);;
      
      modelBuilder.Entity<IndexUri>()
                  .HasOne<ResourceStore>(x => x.ResourceStore)
                  .WithMany(x => x.IndexUriList)
                  .HasForeignKey(x => x.ResourceStoreId)
                  .OnDelete(DeleteBehavior.Cascade);
      
      modelBuilder.Entity<IndexUri>()
                  .HasOne<SearchParameterStore>(x => x.SearchParameterStore)
                  .WithMany()
                  .HasForeignKey(x => x.SearchParameterStoreId)
                  .OnDelete(DeleteBehavior.NoAction);
      
      // ServiceBaseUrl ---------------------------------------------------------------

      
      modelBuilder.Entity<ServiceBaseUrl>().HasKey(x => x.ServiceBaseUrlId);
      // ReSharper disable once EntityNameCapturedOnly.Local
      ServiceBaseUrl serviceBaseUrlForPropertyNameCaptureOnly;
      modelBuilder.Entity<ServiceBaseUrl>().HasIndex(x => new { x.Url, x.IsPrimary })
          .IsUnique()
          .HasFilter($"[{nameof(serviceBaseUrlForPropertyNameCaptureOnly.IsPrimary)}] = 1");

      modelBuilder.Entity<ServiceBaseUrl>().Property(x => x.IsPrimary);
      modelBuilder.Entity<ServiceBaseUrl>().Property(x => x.Url).UseCollation(RepositoryModelConstraints.CaseSensitive);
      
      // ResourceType ---------------------------------------------------------------      
      modelBuilder.Entity<ResourceType>().HasKey(x => x.FhirResourceType);
      
      modelBuilder.Entity<ResourceType>().Property(x => x.FhirResourceType).HasConversion<int>();
      modelBuilder.Entity<ResourceType>().Property(x => x.Name).HasMaxLength(RepositoryModelConstraints.FhirResourceNameMaxLength);

      modelBuilder.Entity<ResourceType>().HasData(
        Enum.GetValues(typeof(FhirResourceTypeId))
            .Cast<FhirResourceTypeId>()
            .Select(e => new ResourceType(e, e.ToString())));

      // HttpVerb -----------------------------------------------------------------      
      modelBuilder.Entity<HttpVerb>().HasKey(x => x.HttpVerbId);
      
      modelBuilder.Entity<HttpVerb>().Property(x => x.HttpVerbId).HasConversion<int>();
      modelBuilder.Entity<HttpVerb>().Property(x => x.Name).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);

      modelBuilder.Entity<HttpVerb>().HasData(
        Enum.GetValues(typeof(HttpVerbId))
            .Cast<HttpVerbId>()
            .Select(e => new HttpVerb(e, e.ToString())));
      
      // PublicationStatus --------------------------------------------------------
      modelBuilder.Entity<PublicationStatus>().HasKey(x => x.PublicationStatusId);
      
      modelBuilder.Entity<PublicationStatus>().Property(x => x.PublicationStatusId).HasConversion<int>();
      modelBuilder.Entity<PublicationStatus>().Property(x => x.Name).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);

      modelBuilder.Entity<PublicationStatus>().HasData(
        Enum.GetValues(typeof(PublicationStatusId))
            .Cast<PublicationStatusId>()
            .Select(e => new PublicationStatus(e, e.ToString())));
      
      // Comparator ---------------------------------------------------------------
      modelBuilder.Entity<Comparator>().HasKey(x => x.SearchComparatorId);
      
      modelBuilder.Entity<Comparator>().Property(x => x.SearchComparatorId).HasConversion<int>();
      modelBuilder.Entity<Comparator>().Property(x => x.Name).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);
      
      modelBuilder.Entity<Comparator>().HasData(
        Enum.GetValues(typeof(SearchComparatorId))
            .Cast<SearchComparatorId>()
            .Select(e => new Comparator(e, e.ToString())));

      // SearchModifierCode ---------------------------------------------------------------
      modelBuilder.Entity<SearchModifierCode>().HasKey(x => x.SearchModifierCodeId);
      
      modelBuilder.Entity<SearchModifierCode>().Property(x => x.SearchModifierCodeId).HasConversion<int>();
      modelBuilder.Entity<SearchModifierCode>().Property(x => x.Name).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);
      
      modelBuilder.Entity<SearchModifierCode>().HasData(
        Enum.GetValues(typeof(SearchModifierCodeId))
            .Cast<SearchModifierCodeId>()
            .Select(e => new SearchModifierCode(e, e.ToString())));
      
      // SearchParameterStore ------------------------------------------------------
      modelBuilder.ApplyConfiguration(new SearchParameterStoreJsonCompressionConversion());
      
      modelBuilder.Entity<SearchParameterStore>().HasKey(x => x.SearchParameterStoreId);
      modelBuilder.Entity<SearchParameterStore>().HasAlternateKey(x => x.ResourceId);
      modelBuilder.Entity<SearchParameterStore>().HasIndex(x => x.VersionId);
      modelBuilder.Entity<SearchParameterStore>().HasIndex(x => x.Code);
      
      modelBuilder.Entity<SearchParameterStore>().Property(x => x.ResourceId).HasMaxLength(RepositoryModelConstraints.FhirIdMaxLength);
      modelBuilder.Entity<SearchParameterStore>().Property(x => x.VersionId).HasMaxLength(RepositoryModelConstraints.FhirIdMaxLength);
      modelBuilder.Entity<SearchParameterStore>().Property(x => x.IsCurrent);
      modelBuilder.Entity<SearchParameterStore>().Property(x => x.IsDeleted);
      modelBuilder.Entity<SearchParameterStore>().Property(x => x.IsIndexed);
      modelBuilder.Entity<SearchParameterStore>().Property(x => x.Name).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);
      modelBuilder.Entity<SearchParameterStore>().Property(x => x.Status).HasConversion<int>();
      modelBuilder.Entity<SearchParameterStore>().Property(x => x.Url);
      modelBuilder.Entity<SearchParameterStore>().Property(x => x.Code).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);
      modelBuilder.Entity<SearchParameterStore>().Property(x => x.Type);
      modelBuilder.Entity<SearchParameterStore>().Property(x => x.Expression);
      modelBuilder.Entity<SearchParameterStore>().Property(x => x.MultipleOr);
      modelBuilder.Entity<SearchParameterStore>().Property(x => x.MultipleAnd);
      modelBuilder.Entity<SearchParameterStore>().Property(x => x.Chain);
      modelBuilder.Entity<SearchParameterStore>().Property(x => x.Json);
      modelBuilder.Entity<SearchParameterStore>().Property(x => x.LastUpdated).HasPrecision(RepositoryModelConstraints.TimestampPrecision); 
      modelBuilder.Entity<SearchParameterStore>().Property(x => x.RowVersion).IsConcurrencyToken();
      
      
      modelBuilder.Entity<SearchParameterStore>()
                  .HasMany<SearchParameterStoreComparator>(x => x.ComparatorList)
                  .WithOne(x => x.SearchParameterStore)
                  .HasForeignKey(x => x.SearchParameterStoreId);

      modelBuilder.Entity<SearchParameterStore>()
                  .HasMany<SearchParameterStoreResourceTypeBase>(x => x.BaseList)
                  .WithOne(x => x.SearchParameterStore)
                  .HasForeignKey(x => x.SearchParameterStoreId);
      
      modelBuilder.Entity<SearchParameterStore>()
                  .HasMany<SearchParameterStoreResourceTypeTarget>(x => x.TargetList)
                  .WithOne(x => x.SearchParameterStore)
                  .HasForeignKey(x => x.SearchParameterStoreId);
      
      modelBuilder.Entity<SearchParameterStore>()
                  .HasMany<SearchParameterStoreSearchModifierCode>(x => x.ModifierList)
                  .WithOne(x => x.SearchParameterStore)
                  .HasForeignKey(x => x.SearchParameterStoreId);
      
      modelBuilder.Entity<SearchParameterStore>()
                  .HasMany<SearchParameterStoreComponent>(x => x.ComponentList)
                  .WithOne(x => x.SearchParameterStore)
                  .HasForeignKey(x => x.SearchParameterStoreId);
      
      modelBuilder.Entity<SearchParameterStore>().HasData(Seed.SearchParameterSeed.Get());
      
      // SearchParameterStoreComparator ---------------------------------------------------------------
      modelBuilder.Entity<SearchParameterStoreComparator>().HasKey(x => x.SearchParameterStoreComparatorId);
      modelBuilder.Entity<SearchParameterStoreComparator>().HasIndex(x => x.SearchParameterStoreId);
      
      modelBuilder.Entity<SearchParameterStoreComparator>().Property(x => x.SearchParameterStoreId);
      modelBuilder.Entity<SearchParameterStoreComparator>().Property(x => x.SearchComparatorId).HasConversion<int>();
      modelBuilder.Entity<SearchParameterStoreComparator>().HasData(Seed.SearchParameterStoreComparatorSeed.Get());
      
      // SearchParameterStoreResourceTypeBase ---------------------------------------------------------------
      modelBuilder.Entity<SearchParameterStoreResourceTypeBase>().HasKey(x => x.SearchParameterStoreResourceTypeBaseId);
      modelBuilder.Entity<SearchParameterStoreResourceTypeBase>().HasIndex(x => x.SearchParameterStoreId);
      
      modelBuilder.Entity<SearchParameterStoreResourceTypeBase>().Property(x => x.SearchParameterStoreId);
      modelBuilder.Entity<SearchParameterStoreResourceTypeBase>().Property(x => x.ResourceType).HasConversion<int>();
      modelBuilder.Entity<SearchParameterStoreResourceTypeBase>().HasData(Seed.SearchParameterStoreResourceTypeBaseSeed.Get());
      
      // SearchParameterStoreResourceTypeTarget ---------------------------------------------------------------
      modelBuilder.Entity<SearchParameterStoreResourceTypeTarget>().HasKey(x => x.SearchParameterStoreResourceTypeTargetId);
      modelBuilder.Entity<SearchParameterStoreResourceTypeTarget>().HasIndex(x => x.SearchParameterStoreId);
      
      modelBuilder.Entity<SearchParameterStoreResourceTypeTarget>().Property(x => x.SearchParameterStoreId);
      modelBuilder.Entity<SearchParameterStoreResourceTypeTarget>().Property(x => x.ResourceType).HasConversion<int>();
      modelBuilder.Entity<SearchParameterStoreResourceTypeTarget>().HasData(Seed.SearchParameterStoreResourceTypeTargetSeed.Get());
      
      // SearchParameterStoreSearchModifierCode ---------------------------------------------------------------
      modelBuilder.Entity<SearchParameterStoreSearchModifierCode>().HasKey(x => x.SearchParameterStoreSearchModifierCodeId);
      modelBuilder.Entity<SearchParameterStoreSearchModifierCode>().HasIndex(x => x.SearchParameterStoreId);
      
      modelBuilder.Entity<SearchParameterStoreSearchModifierCode>().Property(x => x.SearchParameterStoreId);
      modelBuilder.Entity<SearchParameterStoreSearchModifierCode>().Property(x => x.SearchModifierCodeId).HasConversion<int>();
      modelBuilder.Entity<SearchParameterStoreSearchModifierCode>().HasData(Seed.SearchParameterStoreSearchModifierCodeSeed.Get());
      
      // SearchParameterStoreComponent ---------------------------------------------------------------
      modelBuilder.Entity<SearchParameterStoreComponent>().HasKey(x => x.SearchParameterStoreComponentId);
      modelBuilder.Entity<SearchParameterStoreComponent>().HasIndex(x => x.SearchParameterStoreId);
      
      modelBuilder.Entity<SearchParameterStoreComponent>().Property(x => x.SearchParameterStoreId);
      modelBuilder.Entity<SearchParameterStoreComponent>().Property(x => x.Definition);
      modelBuilder.Entity<SearchParameterStoreComponent>().Property(x => x.Expression);
      modelBuilder.Entity<SearchParameterStoreComponent>().HasData(Seed.SearchParameterStoreComponentSeed.Get());
    }
  }


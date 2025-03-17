using Microsoft.EntityFrameworkCore;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Repository.Conversion;
using Abm.Pyro.Repository.EntityConfiguration;

namespace Abm.Pyro.Repository;

public class PyroDbContext : DbContext
{
    public DbSet<ResourceStore> ResourceStore => Set<ResourceStore>();
    public DbSet<IndexString> IndexString => Set<IndexString>();
    public DbSet<IndexReference> IndexReference => Set<IndexReference>();
    public DbSet<SearchParameterStore> SearchParameterStore => Set<SearchParameterStore>();
    public DbSet<ServiceBaseUrl> ServiceBaseUrl => Set<ServiceBaseUrl>();
    public DbSet<ServiceSetting> ServiceSetting => Set<ServiceSetting>();
    
    public PyroDbContext(
        DbContextOptions<PyroDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // EntityTypeConfiguration -------------------------------------------------------------------------------------
        
        //modelBuilder.UseCollation(RepositoryModelConstraints.CaseInsensitive);
        modelBuilder.ApplyConfiguration(new ResourceStoreJsonCompressionConversion());
        modelBuilder.ApplyConfiguration(new SearchParameterStoreJsonCompressionConversion());

        //Entity Configurations ----------------------------------------------------------------------------------------
        
        modelBuilder.ApplyConfiguration(new ServiceSettingEntityConfig());
        modelBuilder.ApplyConfiguration(new ServiceSettingTypeEntityConfig());
        
        modelBuilder.ApplyConfiguration(new ResourceStoreEntityConfig());
        
        modelBuilder.ApplyConfiguration(new IndexStringEntityConfig());
        modelBuilder.ApplyConfiguration(new IndexDateTimeEntityConfig());
        modelBuilder.ApplyConfiguration(new IndexReferenceEntityConfig());
        modelBuilder.ApplyConfiguration(new IndexQuantityEntityConfig());
        modelBuilder.ApplyConfiguration(new IndexTokenEntityConfig());
        modelBuilder.ApplyConfiguration(new IndexUriEntityConfig());

        modelBuilder.ApplyConfiguration(new ServiceBaseUrlEntityConfig());
        
        modelBuilder.ApplyConfiguration(new ResourceTypeEntityConfig());
        modelBuilder.ApplyConfiguration(new HttpVerbEntityConfig());
        modelBuilder.ApplyConfiguration(new PublicationStatusEntityConfig());
        modelBuilder.ApplyConfiguration(new ComparatorEntityConfig());
        modelBuilder.ApplyConfiguration(new SearchModifierCodeEntityConfig());

        modelBuilder.ApplyConfiguration(new SearchParameterStoreEntityConfig());
        modelBuilder.ApplyConfiguration(new SearchParameterStoreComparatorEntityConfig());
        modelBuilder.ApplyConfiguration(new SearchParameterStoreResourceTypeBaseEntityConfig());
        modelBuilder.ApplyConfiguration(new SearchParameterStoreResourceTypeTargetEntityConfig());
        modelBuilder.ApplyConfiguration(new SearchParameterStoreSearchModifierCodeEntityConfig());
        modelBuilder.ApplyConfiguration(new SearchParameterStoreComponentEntityConfig());
    }
}
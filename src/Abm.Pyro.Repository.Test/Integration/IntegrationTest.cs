using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Repository.Query;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Repository.Test.Integration;

public class IntegrationTest : IDisposable, IAsyncDisposable
{
    private readonly PyroDbContext _context;

    public IntegrationTest()
    {
        var options = new DbContextOptionsBuilder<PyroDbContext>()
            .UseSqlServer("Data Source=localhost;Initial Catalog=PyroIntegrationTesting;User ID=sa;Password=AdminPassword123;TrustServerCertificate=True")
            .Options;

        _context = new PyroDbContext(options);
        
        _context.Database.EnsureDeleted();
        _context.Database.EnsureCreated();
        
        _context.ResourceStore.Add(new ResourceStore(
            resourceStoreId: null,
            resourceId: "123",
            versionId: 1,
            resourceType: FhirResourceTypeId.Patient,
            isCurrent: false,
            isDeleted: false,
            httpVerb: HttpVerbId.Post,
            json: "Version 1 Resource JSON",
            lastUpdatedUtc: new DateTime(2020, 1, 1),
            indexReferenceList: new List<IndexReference>(),
            indexStringList: new List<IndexString>(),
            indexDateTimeList: new List<IndexDateTime>(),
            indexQuantityList: new List<IndexQuantity>(),
            indexTokenList: new List<IndexToken>(),
            indexUriList: new List<IndexUri>(),
            rowVersion: 0
        ));
        
        _context.ResourceStore.Add(new ResourceStore(
            resourceStoreId: null,
            resourceId: "123",
            versionId: 2,
            resourceType: FhirResourceTypeId.Patient,
            isCurrent: false,
            isDeleted: true,
            httpVerb: HttpVerbId.Delete,
            json: "Version 2 Resource JSON",
            lastUpdatedUtc: new DateTime(2020, 1, 2),
            indexReferenceList: new List<IndexReference>(),
            indexStringList: new List<IndexString>(),
            indexDateTimeList: new List<IndexDateTime>(),
            indexQuantityList: new List<IndexQuantity>(),
            indexTokenList: new List<IndexToken>(),
            indexUriList: new List<IndexUri>(),
            rowVersion: 0
        ));
        
        _context.ResourceStore.Add(new ResourceStore(
            resourceStoreId: null,
            resourceId: "123",
            versionId: 3,
            resourceType: FhirResourceTypeId.Patient,
            isCurrent: true,
            isDeleted: false,
            httpVerb: HttpVerbId.Put,
            json: "Version 3 Resource JSON",
            lastUpdatedUtc: new DateTime(2020, 1, 3),
            indexReferenceList: new List<IndexReference>(),
            indexStringList: new List<IndexString>(),
            indexDateTimeList: new List<IndexDateTime>(),
            indexQuantityList: new List<IndexQuantity>(),
            indexTokenList: new List<IndexToken>(),
            indexUriList: new List<IndexUri>(),
            rowVersion: 0
        ));

        _context.SaveChanges();

    }
    
    [Fact]
    public async Task ResourceStoreGetByResourceIdTest()
    {
        //Arrange
        var resourceStoreGetByResourceId = new ResourceStoreGetByResourceId(_context);
        
        // Act
        ResourceStore? resourceStore = await resourceStoreGetByResourceId.Get(resourceType: FhirResourceTypeId.Patient, resourceId: "123");
        
        //Assert
        Assert.NotNull(resourceStore);
        Assert.Equal("123", resourceStore.ResourceId);
        Assert.NotNull(resourceStore.ResourceId);
        Assert.Equal(3, resourceStore.VersionId);
        Assert.True(resourceStore.IsCurrent);
        Assert.False(resourceStore.IsDeleted);
        

    }
    
    [Fact]
    public async Task ResourceStoreGetByVersionIdTest()
    {
        //Arrange
        var resourceStoreGetByVersionId = new ResourceStoreGetByVersionId(_context);
        
        // Act
        ResourceStore? resourceStore = await resourceStoreGetByVersionId.Get(
            resourceId: "123", 
            versionId: 1, 
            resourceType: FhirResourceTypeId.Patient);
        
        //Assert
        Assert.NotNull(resourceStore);
        Assert.Equal("123", resourceStore.ResourceId);
        Assert.NotNull(resourceStore.ResourceId);
        Assert.Equal(1, resourceStore.VersionId);
        Assert.False(resourceStore.IsCurrent);
        Assert.False(resourceStore.IsDeleted);
    }
    
    [Fact]
    public async Task ResourceStoreGetForUpdateByResourceIdTest()
    {
        //Arrange
        var resourceStoreGetForUpdateByResourceId = new ResourceStoreGetForUpdateByResourceId(_context);
        
        // Act
        ResourceStoreUpdateProjection? resourceStore = await resourceStoreGetForUpdateByResourceId.Get(
            resourceType: FhirResourceTypeId.Patient,
            resourceId: "123");
        
        //Assert
        Assert.NotNull(resourceStore);
        Assert.NotNull(resourceStore.ResourceStoreId);
        Assert.Equal(3, resourceStore.VersionId);
        Assert.True(resourceStore.IsCurrent);
        Assert.False(resourceStore.IsDeleted);

    }

    
    
    
    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }
}
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirQuery;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Repository.Query;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Repository.Test.Integration;

public class IntegrationTest : IDisposable, IAsyncDisposable
{
    private readonly PyroDbContext _context;
    private readonly int _versionOneResourceStoreId;
    private readonly int _versionTwoResourceStoreId;
    private readonly int _versionThreeResourceStoreId;

    public IntegrationTest()
    {
        var options = new DbContextOptionsBuilder<PyroDbContext>()
            .UseSqlServer("Data Source=localhost;Initial Catalog=PyroIntegrationTesting;User ID=sa;Password=AdminPassword123;TrustServerCertificate=True")
            .Options;

        _context = new PyroDbContext(options);

        _context.Database.EnsureDeleted();
        _context.Database.EnsureCreated();

        var versionOne = new ResourceStore(
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
        );
        _context.ResourceStore.Add(versionOne);

        var versionTwo = new ResourceStore(
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
        );
        _context.ResourceStore.Add(versionTwo);

        var versionThree = new ResourceStore(
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
        );
        _context.ResourceStore.Add(versionThree);

        _context.SaveChanges();

        _versionOneResourceStoreId = versionOne.ResourceStoreId!.Value;
        _versionTwoResourceStoreId = versionTwo.ResourceStoreId!.Value;
        _versionThreeResourceStoreId = versionThree.ResourceStoreId!.Value;
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
    
    [Fact]
    public async Task ResourceStoreGetByResourceStoreIdTest_ValidId_ReturnsRecord()
    {
        var query = new ResourceStoreGetByResourceStoreId(_context);

        ResourceStore? result = await query.Get(_versionThreeResourceStoreId);

        Assert.NotNull(result);
        Assert.Equal(_versionThreeResourceStoreId, result.ResourceStoreId);
        Assert.Equal(3, result.VersionId);
        Assert.True(result.IsCurrent);
        Assert.False(result.IsDeleted);
    }

    [Fact]
    public async Task ResourceStoreGetByResourceStoreIdTest_InvalidId_ReturnsNull()
    {
        var query = new ResourceStoreGetByResourceStoreId(_context);

        ResourceStore? result = await query.Get(-1);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResourceStoreGetHistoryTest_ReturnsAllRecordsOrderedByLastUpdatedDescending()
    {
        var query = new ResourceStoreGetHistory(_context, new StubPaginationSupport());

        ResourceStoreSearchOutcome result = await query.Get(BuildSearchQueryServiceOutcome());

        Assert.Equal(3, result.SearchTotal);
        Assert.Equal(3, result.ResourceStoreList.Count);
        Assert.Equal(3, result.ResourceStoreList[0].VersionId);
        Assert.Equal(2, result.ResourceStoreList[1].VersionId);
        Assert.Equal(1, result.ResourceStoreList[2].VersionId);
    }

    [Fact]
    public async Task ResourceStoreGetHistoryByResourceIdTest_MatchingResourceId_ReturnsAllVersions()
    {
        var query = new ResourceStoreGetHistoryByResourceId(_context, new StubPaginationSupport());

        ResourceStoreSearchOutcome result = await query.Get(FhirResourceTypeId.Patient, "123", BuildSearchQueryServiceOutcome());

        Assert.Equal(3, result.SearchTotal);
        Assert.Equal(3, result.ResourceStoreList.Count);
        Assert.Equal(3, result.ResourceStoreList[0].VersionId);
    }

    [Fact]
    public async Task ResourceStoreGetHistoryByResourceIdTest_NonMatchingResourceId_ReturnsEmpty()
    {
        var query = new ResourceStoreGetHistoryByResourceId(_context, new StubPaginationSupport());

        ResourceStoreSearchOutcome result = await query.Get(FhirResourceTypeId.Patient, "does-not-exist", BuildSearchQueryServiceOutcome());

        Assert.Equal(0, result.SearchTotal);
        Assert.Empty(result.ResourceStoreList);
    }

    [Fact]
    public async Task ResourceStoreGetHistoryByResourceTypeTest_MatchingResourceType_ReturnsAllVersions()
    {
        var query = new ResourceStoreGetHistoryByResourceType(_context, new StubPaginationSupport());

        ResourceStoreSearchOutcome result = await query.Get(FhirResourceTypeId.Patient, BuildSearchQueryServiceOutcome());

        Assert.Equal(3, result.SearchTotal);
        Assert.Equal(3, result.ResourceStoreList.Count);
        Assert.Equal(3, result.ResourceStoreList[0].VersionId);
    }

    [Fact]
    public async Task ResourceStoreGetHistoryByResourceTypeTest_NonMatchingResourceType_ReturnsEmpty()
    {
        var query = new ResourceStoreGetHistoryByResourceType(_context, new StubPaginationSupport());

        ResourceStoreSearchOutcome result = await query.Get(FhirResourceTypeId.Observation, BuildSearchQueryServiceOutcome());

        Assert.Equal(0, result.SearchTotal);
        Assert.Empty(result.ResourceStoreList);
    }

    [Fact]
    public async Task ResourceStoreAddTest_AddsNewRecord_ReturnsPersistedResourceStore()
    {
        var resourceStoreAdd = new ResourceStoreAdd(_context);
        var newResource = new ResourceStore(
            resourceStoreId: null,
            resourceId: "456",
            versionId: 1,
            resourceType: FhirResourceTypeId.Observation,
            isCurrent: true,
            isDeleted: false,
            httpVerb: HttpVerbId.Post,
            json: "New Observation JSON",
            lastUpdatedUtc: new DateTime(2021, 6, 1),
            indexReferenceList: new List<IndexReference>(),
            indexStringList: new List<IndexString>(),
            indexDateTimeList: new List<IndexDateTime>(),
            indexQuantityList: new List<IndexQuantity>(),
            indexTokenList: new List<IndexToken>(),
            indexUriList: new List<IndexUri>(),
            rowVersion: 0
        );

        ResourceStore result = await resourceStoreAdd.Add(newResource);

        Assert.NotNull(result.ResourceStoreId);
        Assert.Equal("456", result.ResourceId);
        Assert.Equal(1, result.VersionId);
        Assert.Equal(FhirResourceTypeId.Observation, result.ResourceType);
        Assert.True(result.IsCurrent);
        Assert.False(result.IsDeleted);
    }

    [Fact]
    public async Task ResourceStoreUpdateTest_WithoutIndexDeletion_UpdatesVersionIdAndIsCurrent()
    {
        var resourceStoreUpdate = new ResourceStoreUpdate(_context);
        var projection = new ResourceStoreUpdateProjection(
            resourceStoreId: _versionOneResourceStoreId,
            versionId: 99,
            isCurrent: true,
            isDeleted: false);

        await resourceStoreUpdate.Update(projection, deleteFhirIndexes: false);

        ResourceStore? updated = await _context.Set<ResourceStore>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ResourceStoreId == _versionOneResourceStoreId);
        Assert.NotNull(updated);
        Assert.Equal(99, updated.VersionId);
        Assert.True(updated.IsCurrent);
    }

    [Fact]
    public async Task ResourceStoreUpdateTest_WithIndexDeletion_DeletesAllIndexesForResource()
    {
        _context.Set<IndexString>().Add(new IndexString(
            indexStringId: null,
            resourceStoreId: _versionOneResourceStoreId,
            resourceStore: null,
            searchParameterStoreId: null,
            searchParameterStore: null,
            value: "test-value"));
        await _context.SaveChangesAsync();

        var resourceStoreUpdate = new ResourceStoreUpdate(_context);
        var projection = new ResourceStoreUpdateProjection(
            resourceStoreId: _versionOneResourceStoreId,
            versionId: 1,
            isCurrent: false,
            isDeleted: false);

        await resourceStoreUpdate.Update(projection, deleteFhirIndexes: true);

        int remainingIndexStrings = await _context.Set<IndexString>()
            .CountAsync(x => x.ResourceStoreId == _versionOneResourceStoreId);
        Assert.Equal(0, remainingIndexStrings);
    }

    private static SearchQueryServiceOutcome BuildSearchQueryServiceOutcome()
    {
        return new SearchQueryServiceOutcome(
            resourceContext: FhirResourceTypeId.Patient,
            fhirQuery: new FhirQuery());
    }

    private sealed class StubPaginationSupport : IPaginationSupport
    {
        private const int DefaultPageSize = 10;

        public int CalculatePageRequired(int? requiredPageNumber, int? countOfRecordsRequested, int totalRecordCount)
            => requiredPageNumber is > 1 ? requiredPageNumber.Value : 1;

        public int CalculateTotalPages(int? countOfRecordsRequested, int totalRecordCount)
        {
            int pageSize = SetNumberOfRecordsPerPage(countOfRecordsRequested);
            if (totalRecordCount == 0 || pageSize == 0) return 1;
            int pages = totalRecordCount / pageSize;
            return (totalRecordCount % pageSize) == 0 ? pages : pages + 1;
        }

        public int SetNumberOfRecordsPerPage(int? countOfRecordsRequested)
            => countOfRecordsRequested ?? DefaultPageSize;

        public Task SetBundlePagination(Bundle bundle, SearchQueryServiceOutcome searchQueryServiceOutcome, string requestSchema, string requestPath, int pagesTotal, int pageCurrentlyRequired)
            => Task.CompletedTask;
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
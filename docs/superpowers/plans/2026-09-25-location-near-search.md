# Location `near` Search Parameter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the FHIR R4 `near` search parameter on `Location`, so `GET [base]/Location?near=[latitude]|[longitude]|[distance]|[units]` returns Locations within the given radius, each carrying its computed distance.

**Architecture:** A new `IndexPosition` table stores each Location's position as a SQL Server `geography` point (SRID 4326) behind a spatial index. Filtering happens through the existing LinqKit predicate pipeline with `STDistance(...) <= @radius`. The distance reported back to the client is computed by a second, page-scoped query (phase 2) and attached to `Bundle.entry.search` as the `location-distance` extension, all behind a configuration flag.

**Tech Stack:** .NET 10, EF Core 10.0.12 (SQL Server), `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite` 10.0.12, NetTopologySuite, LinqKit, Hl7.Fhir.R4 5.11.4, xUnit, Testcontainers, Respawn.

**Spec:** `docs/superpowers/specs/2026-09-25-location-near-search-design.md`

## Global Constraints

- All `dotnet` commands run against `src/Abm.Pyro.CI.slnf`, not `Abm.Pyro.sln`.
- Package versions must be exactly `10.0.12` for all EF Core packages, matching the existing solution.
- Only three distance units are supported: `m`, `km`, and miles (`[mi_i]`, `mi`, `mile`, `miles`). Centimetres, feet, inches and nautical miles are **not** supported.
- Omitted units mean kilometres. Omitted distance means `LocationNearSettings.DefaultDistanceInMetres`.
- Metres is the canonical internal unit everywhere below the parser, because `STDistance` returns metres.
- `new Point(x, y)` is `new Point(longitude, latitude)`. Every construction site sets `SRID = 4326`.
- The parameter value order is normative `[latitude]|[longitude]`. The worked example in FHIR R4 §8.7.5.1 has them swapped; it is a known erratum. Follow the normative order.
- `Location.position.altitude` is ignored throughout.
- No search parameter seed data changes. `Location-near` is already seeded.
- EF model snapshots must stay byte-identical across Windows and Linux; verify with `dotnet ef migrations has-pending-model-changes` after adding the migration.
- Integration tests need Docker running locally.
- Files needing the FHIR model namespace use `using Hl7.Fhir.Model;` plus `using Task = System.Threading.Tasks.Task;`.

## Review Focus

These are the input classes the spec implies but that no task's happy-path tests would otherwise exercise. Each has a test placed in the task that owns the code.

1. **A decimal written with a comma collides with the OR delimiter.** `near=-33,87|151,21` must return 400, not be silently split into two malformed terms and mis-parsed. Test in Task 6.
2. **Antimeridian crossing.** `near=0|179.95|20|km` must match a Location at longitude `-179.95`. This is the specific case a bounding-box implementation gets wrong and `STDistance` gets right. Test in Task 9.
3. **Moving a Location must not leave a stale position index row.** After updating a Location's coordinates, a search near the old position must not find it. Test in Task 9.
4. **Zero or negative distance.** `near=-33.87|151.21|0|km` and `...|-5|km` must return 400, not match everything or throw. Test in Task 6.
5. **Out-of-range coordinates must not break create/update.** A Location with `latitude: 200` must still store successfully with a 201, simply carrying no position index. Test in Task 4.

---

## File Structure

**Created**

| File | Responsibility |
|---|---|
| `src/Abm.Pyro.Domain/Model/IndexPosition.cs` | The index entity holding one Location's geography point |
| `src/Abm.Pyro.Repository/EntityConfiguration/IndexPositionEntityConfig.cs` | EF mapping, keys, FKs, column type |
| `src/Abm.Pyro.Domain/Configuration/LocationNearSettings.cs` | Default radius, max radius, distance-reporting flag |
| `src/Abm.Pyro.Domain/Support/NearDistanceUnitSupport.cs` | The only place unit codes and metre conversions live |
| `src/Abm.Pyro.Domain/IndexSetters/IPositionSetter.cs` | Setter interface |
| `src/Abm.Pyro.Domain/IndexSetters/PositionSetter.cs` | Location.position → IndexPosition row |
| `src/Abm.Pyro.Domain/SearchQueryEntity/SearchQueryNear.cs` | Parses the `near` parameter value |
| `src/Abm.Pyro.Domain/SearchQueryEntity/SearchQueryNearValue.cs` | One parsed position + radius |
| `src/Abm.Pyro.Domain/Query/NearDistance.cs` | A computed distance plus the unit to report it in |
| `src/Abm.Pyro.Repository/Predicates/IIndexPositionPredicateFactory.cs` | Predicate factory interface |
| `src/Abm.Pyro.Repository/Predicates/IndexPositionPredicateFactory.cs` | Builds the `STDistance <= radius` predicate |
| `src/Abm.Pyro.Repository/Query/INearDistanceQuery.cs` | Phase 2 interface |
| `src/Abm.Pyro.Repository/Query/NearDistanceQuery.cs` | Phase 2: page-scoped distance computation |
| `src/Abm.Pyro.Domain.Test/IndexSetters/PositionSetterTest.cs` | Setter unit tests |
| `src/Abm.Pyro.Domain.Test/SearchQueryEntity/SearchQueryNearTest.cs` | Parser unit tests |
| `src/Abm.Pyro.Domain.Test/Support/NearDistanceUnitSupportTest.cs` | Unit conversion tests |
| `src/Abm.Pyro.Api.Test/Support/LocationBuilder.cs` | Test Location factory |
| `src/Abm.Pyro.Api.Test/Search/NearSearchTests.cs` | Integration tests for filtering |
| `src/Abm.Pyro.Api.Test/Search/NearDistanceExtensionTests.cs` | Integration tests for the distance extension |
| `src/Abm.Pyro.Api.Test/Search/NearChainedSearchTests.cs` | Integration tests for chained and `_has` use |

**Modified**

| File | Change |
|---|---|
| `src/Abm.Pyro.Domain/Abm.Pyro.Domain.csproj` | + `NetTopologySuite` |
| `src/Abm.Pyro.Repository/Abm.Pyro.Repository.csproj` | + `…SqlServer.NetTopologySuite` 10.0.12 |
| `src/Abm.Pyro.Api/Abm.Pyro.Api.csproj` | + `…SqlServer.NetTopologySuite` 10.0.12 |
| `src/Abm.Pyro.Domain/Model/ResourceStore.cs` | + `IndexPositionList` |
| `src/Abm.Pyro.Domain/Indexing/IndexerOutcome.cs` | + `PositionIndexList` |
| `src/Abm.Pyro.Repository/PyroDbContext.cs` | + DbSet and config registration |
| `src/Abm.Pyro.Api/DependencyInjectionFactory/PyroDbContextFactory.cs` | + `UseNetTopologySuite()` |
| `src/Abm.Pyro.Api/Program.cs` | + `UseNetTopologySuite()`, + DI registrations |
| `src/Abm.Pyro.Repository/DesignTimeDbContextFactory.cs` | + `UseNetTopologySuite()` |
| `src/Abm.Pyro.Api.Test/Fixtures/IntegrationTestFixture.cs` | + `UseNetTopologySuite()`, + Respawn table |
| `src/Abm.Pyro.Api/appsettings.json` | + `LocationNear` section |
| `src/Abm.Pyro.Api/Extensions/ConfigurationSettingsExtension.cs` | + options registration |
| `src/Abm.Pyro.Application/Indexing/Indexer.cs` | Special arm dispatches to the position setter |
| `src/Abm.Pyro.Application/FhirHandler/FhirCreateHandler.cs` | Pass position index list |
| `src/Abm.Pyro.Application/FhirHandler/FhirUpdateHandler.cs` | Pass position index list |
| `src/Abm.Pyro.Application/FhirHandler/FhirPatchHandler.cs` | Pass position index list |
| `src/Abm.Pyro.Domain/SearchQuery/SearchQueryFactory.cs` | Special arm dispatches on canonical URL |
| `src/Abm.Pyro.Repository/Predicates/IResourceStorePredicateFactory.cs` | + `PositionIndex` |
| `src/Abm.Pyro.Repository/Predicates/ResourceStorePredicateFactory.cs` | + `PositionIndex` |
| `src/Abm.Pyro.Repository/Predicates/SearchSearchPredicateFactory.cs` | Special arm builds a real predicate |
| `src/Abm.Pyro.Domain/Query/ResourceStoreSearchOutcome.cs` | + distance map |
| `src/Abm.Pyro.Repository/Query/ResourceStoreSearch.cs` | Runs phase 2 |
| `src/Abm.Pyro.Domain/FhirSupport/FhirBundleCreationCreationSupport.cs` | Emits the extension |
| `CLAUDE.md` | Document the feature |

---

## Task 1: Spike — prove LinqKit composes with NTS into index-using SQL

**Throwaway.** Nothing in this task except the package references survives. Its output is an answer, recorded in the plan, not code we keep.

The question: does LinqKit's `.Any(predicate.Compile())` expansion compose with an NTS `Point.Distance()` call and still produce SQL that *uses* the spatial index? LinqKit expression expansion and EF spatial translation are independent mechanisms and have never been exercised together in this codebase. If the answer is no, stop and revisit the storage decision before Task 2.

**Files:**
- Modify: `src/Abm.Pyro.Domain/Abm.Pyro.Domain.csproj`
- Modify: `src/Abm.Pyro.Repository/Abm.Pyro.Repository.csproj`
- Modify: `src/Abm.Pyro.Api/Abm.Pyro.Api.csproj`
- Create (throwaway): `src/Abm.Pyro.Api.Test/Spike/SpatialIndexSpike.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: package references only. No types survive this task.

- [ ] **Step 1: Add the packages**

In `src/Abm.Pyro.Domain/Abm.Pyro.Domain.csproj`, inside the existing `<ItemGroup>` of `PackageReference` entries:

```xml
<PackageReference Include="NetTopologySuite" Version="2.6.0" />
```

In both `src/Abm.Pyro.Repository/Abm.Pyro.Repository.csproj` and `src/Abm.Pyro.Api/Abm.Pyro.Api.csproj`:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite" Version="10.0.12" />
```

If `NetTopologySuite` 2.6.0 does not resolve, use whichever version `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite` 10.0.12 pulls in transitively — run `dotnet list src/Abm.Pyro.Repository/Abm.Pyro.Repository.csproj package --include-transitive` and match it exactly.

- [ ] **Step 2: Verify the solution still builds**

Run: `dotnet build src/Abm.Pyro.CI.slnf`
Expected: succeeds.

- [ ] **Step 3: Write the spike test**

Create `src/Abm.Pyro.Api.Test/Spike/SpatialIndexSpike.cs`. This builds a miniature of the real shape — a parent table with a collection navigation to a child table holding a `geography` point — because the risk lives specifically in the `parent.ChildList.Any(...)` form, not in a bare `Where`.

```csharp
using System.Linq.Expressions;
using Abm.Pyro.Api.Test.Fixtures;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Spike;

public class SpikeParent
{
    public int SpikeParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<SpikeChild> ChildList { get; set; } = new();
}

public class SpikeChild
{
    public int SpikeChildId { get; set; }
    public int SpikeParentId { get; set; }
    public SpikeParent? Parent { get; set; }
    public Point Position { get; set; } = null!;
}

public class SpikeDbContext(DbContextOptions<SpikeDbContext> options) : DbContext(options)
{
    public DbSet<SpikeParent> SpikeParent => Set<SpikeParent>();
    public DbSet<SpikeChild> SpikeChild => Set<SpikeChild>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SpikeParent>().HasKey(x => x.SpikeParentId);
        modelBuilder.Entity<SpikeChild>().HasKey(x => x.SpikeChildId);
        modelBuilder.Entity<SpikeChild>().Property(x => x.Position).HasColumnType("geography");
        modelBuilder.Entity<SpikeChild>()
            .HasOne(x => x.Parent).WithMany(x => x.ChildList).HasForeignKey(x => x.SpikeParentId);
    }
}

public class SpatialIndexSpike(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task LinqKitAnyWithNtsDistance_TranslatesToStDistanceSql()
    {
        var options = new DbContextOptionsBuilder<SpikeDbContext>()
            .UseSqlServer(Fixture.ConnectionString, o => o.UseNetTopologySuite())
            .Options;

        await using var context = new SpikeDbContext(options);

        await context.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('SpikeChild') IS NOT NULL DROP TABLE SpikeChild;
            IF OBJECT_ID('SpikeParent') IS NOT NULL DROP TABLE SpikeParent;
            CREATE TABLE SpikeParent (
                SpikeParentId INT IDENTITY(1,1) NOT NULL PRIMARY KEY CLUSTERED,
                Name NVARCHAR(100) NOT NULL);
            CREATE TABLE SpikeChild (
                SpikeChildId INT IDENTITY(1,1) NOT NULL PRIMARY KEY CLUSTERED,
                SpikeParentId INT NOT NULL REFERENCES SpikeParent(SpikeParentId),
                Position GEOGRAPHY NOT NULL);
            CREATE SPATIAL INDEX SPATIAL_SpikeChild_Position
                ON SpikeChild(Position) USING GEOGRAPHY_AUTO_GRID;
            """);

        // Sydney Opera House
        var target = new Point(151.2153, -33.8568) { SRID = 4326 };

        context.SpikeParent.Add(new SpikeParent
        {
            Name = "near",
            ChildList = { new SpikeChild { Position = new Point(151.2100, -33.8600) { SRID = 4326 } } }
        });
        context.SpikeParent.Add(new SpikeParent
        {
            Name = "far",
            ChildList = { new SpikeChild { Position = new Point(144.9631, -37.8136) { SRID = 4326 } } }
        });
        await context.SaveChangesAsync();

        // The exact shape SearchSearchPredicateFactory uses.
        Expression<Func<SpikeChild, bool>> childPredicate =
            c => c.Position.Distance(target) <= 5000d;

        var predicate = PredicateBuilder.New<SpikeParent>(true);
        predicate = predicate.Or(p => p.ChildList.Any(childPredicate.Compile()));

        IQueryable<SpikeParent> query = context.SpikeParent.AsExpandable().Where(predicate);

        string sql = query.ToQueryString();
        List<SpikeParent> results = await query.ToListAsync();

        Assert.Contains("STDistance", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Single(results);
        Assert.Equal("near", results[0].Name);
    }
}
```

- [ ] **Step 4: Run the spike and record the answer**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter FullyQualifiedName~SpatialIndexSpike`

Three outcomes:
- **PASS** — LinqKit and NTS compose. Continue to Step 5.
- **FAIL, SQL has no `STDistance`** (e.g. a client-side evaluation exception, or `.Compile()` executed in memory) — LinqKit expansion does not survive. **Stop. Report to the human partner before continuing.** The predicate architecture, not the storage, needs rethinking.
- **FAIL, other** — diagnose, but do not proceed past this task until the assertions pass.

- [ ] **Step 5: Confirm the spatial index is actually used**

The SQL translating is necessary but not sufficient; it must also seek the spatial index rather than scan. Capture the plan:

```bash
docker ps --filter "ancestor=mcr.microsoft.com/mssql/server:2022-latest" --format "{{.Names}}"
```

Connect to that container's SQL Server with the fixture's connection string and run:

```sql
SET SHOWPLAN_TEXT ON;
GO
SELECT * FROM SpikeParent p
WHERE EXISTS (SELECT 1 FROM SpikeChild c
              WHERE c.SpikeParentId = p.SpikeParentId
                AND c.Position.STDistance(geography::Point(-33.8568, 151.2153, 4326)) <= 5000);
GO
```

Expected: the plan references `SPATIAL_SpikeChild_Position`.

If the plan shows a full table scan instead, that is **not** a blocker on its own — at the row counts in this test SQL Server will prefer a scan regardless. Record the finding and continue; note it for the human partner as something to re-measure against production-scale data.

- [ ] **Step 6: Delete the spike and commit the packages**

```bash
rm -rf src/Abm.Pyro.Api.Test/Spike
git add src/Abm.Pyro.Domain/Abm.Pyro.Domain.csproj src/Abm.Pyro.Repository/Abm.Pyro.Repository.csproj src/Abm.Pyro.Api/Abm.Pyro.Api.csproj
git commit -m "chore: add NetTopologySuite packages for Location near search

Spike confirmed LinqKit .Any(predicate.Compile()) composes with NTS
Point.Distance() and translates to STDistance SQL.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 2: IndexPosition table and migration

**Files:**
- Create: `src/Abm.Pyro.Domain/Model/IndexPosition.cs`
- Create: `src/Abm.Pyro.Repository/EntityConfiguration/IndexPositionEntityConfig.cs`
- Modify: `src/Abm.Pyro.Domain/Model/ResourceStore.cs`
- Modify: `src/Abm.Pyro.Repository/PyroDbContext.cs`
- Modify: `src/Abm.Pyro.Api/DependencyInjectionFactory/PyroDbContextFactory.cs:25`
- Modify: `src/Abm.Pyro.Api/Program.cs:357`
- Modify: `src/Abm.Pyro.Repository/DesignTimeDbContextFactory.cs:34`
- Modify: `src/Abm.Pyro.Api.Test/Fixtures/IntegrationTestFixture.cs:29` and its `TablesToInclude`

**Interfaces:**
- Consumes: the NTS packages from Task 1.
- Produces:
  - `Abm.Pyro.Domain.Model.IndexPosition` with ctor
    `IndexPosition(int? indexPositionId, int? resourceStoreId, ResourceStore? resourceStore, int? searchParameterStoreId, SearchParameterStore? searchParameterStore, Point position)`
    and properties `int? IndexPositionId`, `Point Position`.
  - `ResourceStore.IndexPositionList` of type `List<IndexPosition>`.
  - `ResourceStore` ctor gains a trailing optional parameter `List<IndexPosition>? indexPositionList = null`.

- [ ] **Step 1: Create the entity**

`src/Abm.Pyro.Domain/Model/IndexPosition.cs`:

```csharp
using NetTopologySuite.Geometries;

#pragma warning disable CS8618
namespace Abm.Pyro.Domain.Model;

public class IndexPosition : IndexBase
{
  private IndexPosition() : base()
  {
  }

  public IndexPosition(int? indexPositionId, int? resourceStoreId, ResourceStore? resourceStore,
                       int? searchParameterStoreId, SearchParameterStore? searchParameterStore,
                       Point position)
    : base(resourceStoreId, resourceStore, searchParameterStoreId, searchParameterStore)
  {
    IndexPositionId = indexPositionId;
    Position = position;
  }

  public int? IndexPositionId { get; set; }

  /// <summary>
  /// The Location's WGS84 position, stored as a SQL Server geography point with SRID 4326.
  /// Note the coordinate order: X is longitude and Y is latitude, which is the opposite
  /// of T-SQL's geography::Point(latitude, longitude, srid).
  /// </summary>
  public Point Position { get; set; }
}
```

- [ ] **Step 2: Add the navigation property to ResourceStore**

In `src/Abm.Pyro.Domain/Model/ResourceStore.cs`, add a trailing optional constructor parameter after `int rowVersion`:

```csharp
                       int rowVersion,
                       List<IndexPosition>? indexPositionList = null)
```

and in the constructor body, after `RowVersion = rowVersion;`:

```csharp
    IndexPositionList = indexPositionList ?? new List<IndexPosition>();
```

and alongside the other list properties:

```csharp
  public List<IndexPosition> IndexPositionList { get; set; }
```

It is optional and trailing so that the eight existing call sites that never index a position — tests, history queries — need no change. Only the three FHIR handlers pass it.

- [ ] **Step 3: Create the entity configuration**

`src/Abm.Pyro.Repository/EntityConfiguration/IndexPositionEntityConfig.cs`, mirroring `IndexQuantityEntityConfig`:

```csharp
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
```

- [ ] **Step 4: Register in the DbContext**

In `src/Abm.Pyro.Repository/PyroDbContext.cs`, add the DbSet alongside the others:

```csharp
    public DbSet<IndexPosition> IndexPosition => Set<IndexPosition>();
```

and the configuration, after `IndexUriEntityConfig`:

```csharp
        modelBuilder.ApplyConfiguration(new IndexPositionEntityConfig());
```

- [ ] **Step 5: Enable NetTopologySuite at all four UseSqlServer call sites**

`src/Abm.Pyro.Api/DependencyInjectionFactory/PyroDbContextFactory.cs:25`:

```csharp
        dbContextOptionsBuilder.UseSqlServer(connectionString, o => o.UseNetTopologySuite())
```

`src/Abm.Pyro.Api/Program.cs:357`:

```csharp
                .UseSqlServer(builder.Configuration.GetConnectionString(tenantService.GetScopedTenant().SqlConnectionStringCode),
                    o => o.UseNetTopologySuite())
```

`src/Abm.Pyro.Repository/DesignTimeDbContextFactory.cs:34`:

```csharp
      .UseSqlServer(configuration.GetConnectionString("PyroDb"), o => o.UseNetTopologySuite());
```

`src/Abm.Pyro.Api.Test/Fixtures/IntegrationTestFixture.cs:29`:

```csharp
            .UseSqlServer(ConnectionString, o => o.UseNetTopologySuite());
```

Missing any one of these produces a confusing runtime failure, because the model builds but the provider cannot map `Point`.

- [ ] **Step 6: Add IndexPosition to the Respawn table list**

In `src/Abm.Pyro.Api.Test/Fixtures/IntegrationTestFixture.cs`, in `TablesToInclude` after `new Respawn.Graph.Table("IndexUri"),`:

```csharp
                new Respawn.Graph.Table("IndexPosition"),
```

- [ ] **Step 7: Create the migration**

```bash
cd src
dotnet ef migrations add AddIndexPositionTable --project Abm.Pyro.Repository --startup-project Abm.Pyro.Repository
```

- [ ] **Step 8: Add the spatial index to the migration by hand**

EF cannot model a spatial index. Open the generated migration in `src/Abm.Pyro.Repository/Migrations/`. At the **end** of `Up`, after the table and its indexes are created:

```csharp
            migrationBuilder.Sql(
                "CREATE SPATIAL INDEX SPATIAL_IndexPosition_Position " +
                "ON IndexPosition(Position) USING GEOGRAPHY_AUTO_GRID;");
```

At the **start** of `Down`, before the table is dropped:

```csharp
            migrationBuilder.Sql("DROP INDEX SPATIAL_IndexPosition_Position ON IndexPosition;");
```

- [ ] **Step 9: Verify the snapshot is clean**

```bash
cd src
dotnet ef migrations has-pending-model-changes --project Abm.Pyro.Repository --startup-project Abm.Pyro.Repository
```

Expected: "No changes have been made to the model since the last migration."

- [ ] **Step 10: Verify the migration applies against a real SQL Server**

Run the existing integration suite, which applies migrations to the Testcontainers instance before starting:

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj`
Expected: PASS. The suite has no `near` tests yet; this proves the migration and the geography mapping do not break anything that already worked.

- [ ] **Step 11: Commit**

```bash
git add src/Abm.Pyro.Domain src/Abm.Pyro.Repository src/Abm.Pyro.Api src/Abm.Pyro.Api.Test
git commit -m "feat: add IndexPosition table with geography column and spatial index

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 3: Distance units and configuration

**Files:**
- Create: `src/Abm.Pyro.Domain/Support/NearDistanceUnitSupport.cs`
- Create: `src/Abm.Pyro.Domain/Configuration/LocationNearSettings.cs`
- Create: `src/Abm.Pyro.Domain.Test/Support/NearDistanceUnitSupportTest.cs`
- Modify: `src/Abm.Pyro.Api/appsettings.json`
- Modify: `src/Abm.Pyro.Api/Extensions/ConfigurationSettingsExtension.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces:
  - `enum NearDistanceUnit { Metre, Kilometre, Mile }` in `Abm.Pyro.Domain.Support`.
  - `static class NearDistanceUnitSupport` with
    `bool TryParse(string? unitCode, out NearDistanceUnit unit)`,
    `double ToMetres(decimal distance, NearDistanceUnit unit)`,
    `double FromMetres(double metres, NearDistanceUnit unit)`,
    `string UcumCode(NearDistanceUnit unit)`,
    `string DisplayUnit(NearDistanceUnit unit)`,
    `const string SupportedUnitsMessage`.
  - `LocationNearSettings` with `const string SectionName = "LocationNear"`, `int DefaultDistanceInMetres`, `int MaximumDistanceInMetres`, `bool ReturnDistanceInSearchResults`.

- [ ] **Step 1: Write the failing unit conversion tests**

`src/Abm.Pyro.Domain.Test/Support/NearDistanceUnitSupportTest.cs`:

```csharp
using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Domain.Test.Support;

public class NearDistanceUnitSupportTest
{
    [Theory]
    [InlineData("km", NearDistanceUnit.Kilometre)]
    [InlineData("KM", NearDistanceUnit.Kilometre)]
    [InlineData("m", NearDistanceUnit.Metre)]
    [InlineData("[mi_i]", NearDistanceUnit.Mile)]
    [InlineData("mi", NearDistanceUnit.Mile)]
    [InlineData("mile", NearDistanceUnit.Mile)]
    [InlineData("miles", NearDistanceUnit.Mile)]
    [InlineData("", NearDistanceUnit.Kilometre)]
    [InlineData(null, NearDistanceUnit.Kilometre)]
    public void TryParse_SupportedUnit_ReturnsTrueAndExpectedUnit(string? unitCode, NearDistanceUnit expected)
    {
        bool result = NearDistanceUnitSupport.TryParse(unitCode, out NearDistanceUnit unit);

        Assert.True(result);
        Assert.Equal(expected, unit);
    }

    [Theory]
    [InlineData("cm")]
    [InlineData("ft")]
    [InlineData("[ft_i]")]
    [InlineData("in")]
    [InlineData("nmi")]
    [InlineData("[nmi_i]")]
    [InlineData("furlong")]
    public void TryParse_UnsupportedUnit_ReturnsFalse(string unitCode)
    {
        bool result = NearDistanceUnitSupport.TryParse(unitCode, out _);

        Assert.False(result);
    }

    [Theory]
    [InlineData(1, NearDistanceUnit.Metre, 1d)]
    [InlineData(1, NearDistanceUnit.Kilometre, 1000d)]
    [InlineData(1, NearDistanceUnit.Mile, 1609.344d)]
    [InlineData(11.2, NearDistanceUnit.Kilometre, 11200d)]
    public void ToMetres_ConvertsCorrectly(decimal distance, NearDistanceUnit unit, double expectedMetres)
    {
        double result = NearDistanceUnitSupport.ToMetres(distance, unit);

        Assert.Equal(expectedMetres, result, 6);
    }

    [Theory]
    [InlineData(1609.344d, NearDistanceUnit.Mile, 1d)]
    [InlineData(1000d, NearDistanceUnit.Kilometre, 1d)]
    [InlineData(2500d, NearDistanceUnit.Metre, 2500d)]
    public void FromMetres_IsTheInverseOfToMetres(double metres, NearDistanceUnit unit, double expected)
    {
        double result = NearDistanceUnitSupport.FromMetres(metres, unit);

        Assert.Equal(expected, result, 6);
    }

    [Theory]
    [InlineData(NearDistanceUnit.Metre, "m", "m")]
    [InlineData(NearDistanceUnit.Kilometre, "km", "km")]
    [InlineData(NearDistanceUnit.Mile, "[mi_i]", "mi")]
    public void UcumCodeAndDisplayUnit_AreCorrect(NearDistanceUnit unit, string expectedCode, string expectedDisplay)
    {
        Assert.Equal(expectedCode, NearDistanceUnitSupport.UcumCode(unit));
        Assert.Equal(expectedDisplay, NearDistanceUnitSupport.DisplayUnit(unit));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Abm.Pyro.Domain.Test/Abm.Pyro.Domain.Test.csproj --filter FullyQualifiedName~NearDistanceUnitSupportTest`
Expected: FAIL to compile — `NearDistanceUnitSupport` does not exist.

- [ ] **Step 3: Implement the unit support**

`src/Abm.Pyro.Domain/Support/NearDistanceUnitSupport.cs`:

```csharp
namespace Abm.Pyro.Domain.Support;

/// <summary>
/// The distance units accepted by the Location 'near' search parameter.
/// FHIR R4 specifies UCUM codes and says kilometres are assumed when units are omitted.
/// This server deliberately supports metres, kilometres and miles only.
/// </summary>
public enum NearDistanceUnit
{
  Metre,
  Kilometre,
  Mile
}

public static class NearDistanceUnitSupport
{
  private const double MetresPerKilometre = 1000d;
  private const double MetresPerMile = 1609.344d;

  public const string SupportedUnitsMessage =
    "Supported units are 'm' (metres), 'km' (kilometres) and '[mi_i]', 'mi', 'mile' or 'miles' (miles). " +
    "When the units are omitted, kilometres are assumed.";

  /// <summary>
  /// Parses a unit code from the fourth segment of a 'near' search parameter value.
  /// A null or empty code means kilometres, per the FHIR R4 specification.
  /// </summary>
  public static bool TryParse(string? unitCode, out NearDistanceUnit unit)
  {
    if (string.IsNullOrWhiteSpace(unitCode))
    {
      unit = NearDistanceUnit.Kilometre;
      return true;
    }

    switch (unitCode.Trim().ToLowerInvariant())
    {
      case "m":
        unit = NearDistanceUnit.Metre;
        return true;
      case "km":
        unit = NearDistanceUnit.Kilometre;
        return true;
      case "[mi_i]":
      case "mi":
      case "mile":
      case "miles":
        unit = NearDistanceUnit.Mile;
        return true;
      default:
        unit = NearDistanceUnit.Kilometre;
        return false;
    }
  }

  public static double ToMetres(decimal distance, NearDistanceUnit unit)
  {
    double value = (double)distance;
    return unit switch
    {
      NearDistanceUnit.Metre => value,
      NearDistanceUnit.Kilometre => value * MetresPerKilometre,
      NearDistanceUnit.Mile => value * MetresPerMile,
      _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
    };
  }

  public static double FromMetres(double metres, NearDistanceUnit unit)
  {
    return unit switch
    {
      NearDistanceUnit.Metre => metres,
      NearDistanceUnit.Kilometre => metres / MetresPerKilometre,
      NearDistanceUnit.Mile => metres / MetresPerMile,
      _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
    };
  }

  /// <summary>The UCUM code to place in Quantity.code.</summary>
  public static string UcumCode(NearDistanceUnit unit)
  {
    return unit switch
    {
      NearDistanceUnit.Metre => "m",
      NearDistanceUnit.Kilometre => "km",
      NearDistanceUnit.Mile => "[mi_i]",
      _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
    };
  }

  /// <summary>The human-readable unit to place in Quantity.unit.</summary>
  public static string DisplayUnit(NearDistanceUnit unit)
  {
    return unit switch
    {
      NearDistanceUnit.Metre => "m",
      NearDistanceUnit.Kilometre => "km",
      NearDistanceUnit.Mile => "mi",
      _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
    };
  }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/Abm.Pyro.Domain.Test/Abm.Pyro.Domain.Test.csproj --filter FullyQualifiedName~NearDistanceUnitSupportTest`
Expected: PASS.

- [ ] **Step 5: Create the settings class**

`src/Abm.Pyro.Domain/Configuration/LocationNearSettings.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Abm.Pyro.Domain.Configuration;

public sealed class LocationNearSettings : IValidatableObject
{
  public const string SectionName = "LocationNear";

  /// <summary>
  /// Half the earth's circumference; the largest meaningful great-circle distance.
  /// </summary>
  private const int MaximumSupportedDistanceInMetres = 20_000_000;

  /// <summary>
  /// The search radius used when a client omits the [distance] segment of a 'near' search
  /// parameter value, for example 'Location?near=-33.8568|151.2153'. FHIR R4 leaves this to
  /// the server's discretion.
  /// </summary>
  [Range(1, MaximumSupportedDistanceInMetres, ErrorMessage = "Can only be between 1 .. 20000000")]
  public int DefaultDistanceInMetres { get; init; } = 10_000;

  /// <summary>
  /// The largest radius a client may request. A request above this is rejected with a 400 so
  /// that a single search cannot scan the whole position index.
  /// </summary>
  [Range(1, MaximumSupportedDistanceInMetres, ErrorMessage = "Can only be between 1 .. 20000000")]
  public int MaximumDistanceInMetres { get; init; } = 1_000_000;

  /// <summary>
  /// When true, each matched Location in a search result carries its distance from the
  /// requested point as a 'location-distance' extension on Bundle.entry.search. This costs one
  /// extra page-scoped database query per near search. Turning it off does not change which
  /// Locations match, only whether the response reports how far away they are.
  /// </summary>
  public bool ReturnDistanceInSearchResults { get; init; } = true;

  public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
  {
    if (DefaultDistanceInMetres > MaximumDistanceInMetres)
    {
      yield return new ValidationResult(
        $"{nameof(DefaultDistanceInMetres)} ({DefaultDistanceInMetres}) must not be greater than " +
        $"{nameof(MaximumDistanceInMetres)} ({MaximumDistanceInMetres}).",
        new[] { nameof(DefaultDistanceInMetres), nameof(MaximumDistanceInMetres) });
    }
  }
}
```

- [ ] **Step 6: Register the settings and add the appsettings section**

In `src/Abm.Pyro.Api/Extensions/ConfigurationSettingsExtension.cs`, after the `IndexingSettings` block:

```csharp
        services.AddOptions<LocationNearSettings>()
            .Bind(configuration.GetSection(LocationNearSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
```

In `src/Abm.Pyro.Api/appsettings.json`, after the `"Indexing"` section:

```json
  "LocationNear": {
    "DefaultDistanceInMetres": 10000,
    "MaximumDistanceInMetres": 1000000,
    "ReturnDistanceInSearchResults": true
  },
```

- [ ] **Step 7: Verify the server still starts**

Run: `dotnet build src/Abm.Pyro.CI.slnf`
Expected: succeeds.

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter FullyQualifiedName~CreateTests`
Expected: PASS. `ValidateOnStart` means a bad settings class fails host startup, so any integration test starting the host proves the registration is valid.

- [ ] **Step 8: Commit**

```bash
git add src/Abm.Pyro.Domain src/Abm.Pyro.Domain.Test src/Abm.Pyro.Api
git commit -m "feat: add near distance units and LocationNear configuration

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 4: PositionSetter

**Files:**
- Create: `src/Abm.Pyro.Domain/IndexSetters/IPositionSetter.cs`
- Create: `src/Abm.Pyro.Domain/IndexSetters/PositionSetter.cs`
- Create: `src/Abm.Pyro.Domain.Test/IndexSetters/PositionSetterTest.cs`

**Interfaces:**
- Consumes: `IndexPosition` from Task 2.
- Produces: `IPositionSetter` with
  `IList<IndexPosition> Set(ITypedElement typedElement, FhirResourceTypeId resourceType, int searchParameterId, string searchParameterName)`,
  matching the signature of the seven existing setters.

- [ ] **Step 1: Write the failing setter tests**

`src/Abm.Pyro.Domain.Test/IndexSetters/PositionSetterTest.cs`. Note the ordering test — it is the one guard against the highest-risk defect in this feature.

```csharp
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.IndexSetters;
using Abm.Pyro.Domain.Model;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging.Abstractions;

namespace Abm.Pyro.Domain.Test.IndexSetters;

public class PositionSetterTest
{
    private const int SearchParameterId = 758;
    private const string SearchParameterName = "near";

    private static ITypedElement PositionElement(decimal? latitude, decimal? longitude)
    {
        var position = new Location.PositionComponent
        {
            LatitudeElement = latitude.HasValue ? new FhirDecimal(latitude.Value) : null,
            LongitudeElement = longitude.HasValue ? new FhirDecimal(longitude.Value) : null
        };

        return position.ToTypedElement(ModelInfo.ModelInspector);
    }

    private static PositionSetter CreateSut() =>
        new PositionSetter(NullLogger<PositionSetter>.Instance);

    [Fact]
    public void Set_ValidPosition_ReturnsSingleIndexRow()
    {
        IList<IndexPosition> result = CreateSut().Set(
            PositionElement(-33.8568m, 151.2153m),
            FhirResourceTypeId.Location, SearchParameterId, SearchParameterName);

        Assert.Single(result);
        Assert.Equal(SearchParameterId, result[0].SearchParameterStoreId);
        Assert.Equal(4326, result[0].Position.SRID);
    }

    /// <summary>
    /// NetTopologySuite's Point(x, y) is Point(longitude, latitude), the opposite of T-SQL's
    /// geography::Point(latitude, longitude, srid). Getting this backwards puts every Location
    /// somewhere else on earth without any error, so it is asserted explicitly.
    /// </summary>
    [Fact]
    public void Set_ValidPosition_PutsLongitudeInXAndLatitudeInY()
    {
        IList<IndexPosition> result = CreateSut().Set(
            PositionElement(-33.8568m, 151.2153m),
            FhirResourceTypeId.Location, SearchParameterId, SearchParameterName);

        Assert.Equal(151.2153d, result[0].Position.X, 6);
        Assert.Equal(-33.8568d, result[0].Position.Y, 6);
    }

    [Theory]
    [InlineData(null, 151.2153)]
    [InlineData(-33.8568, null)]
    [InlineData(null, null)]
    public void Set_MissingCoordinate_ReturnsNoRows(double? latitude, double? longitude)
    {
        IList<IndexPosition> result = CreateSut().Set(
            PositionElement((decimal?)latitude, (decimal?)longitude),
            FhirResourceTypeId.Location, SearchParameterId, SearchParameterName);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(200, 151.2153)]   // latitude above 90
    [InlineData(-91, 151.2153)]   // latitude below -90
    [InlineData(-33.8568, 181)]   // longitude above 180
    [InlineData(-33.8568, -181)]  // longitude below -180
    public void Set_OutOfRangeCoordinate_ReturnsNoRowsAndDoesNotThrow(decimal latitude, decimal longitude)
    {
        IList<IndexPosition> result = CreateSut().Set(
            PositionElement(latitude, longitude),
            FhirResourceTypeId.Location, SearchParameterId, SearchParameterName);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(90, 180)]
    [InlineData(-90, -180)]
    [InlineData(0, 0)]
    public void Set_CoordinateAtTheLimit_ReturnsSingleIndexRow(decimal latitude, decimal longitude)
    {
        IList<IndexPosition> result = CreateSut().Set(
            PositionElement(latitude, longitude),
            FhirResourceTypeId.Location, SearchParameterId, SearchParameterName);

        Assert.Single(result);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Abm.Pyro.Domain.Test/Abm.Pyro.Domain.Test.csproj --filter FullyQualifiedName~PositionSetterTest`
Expected: FAIL to compile — `PositionSetter` does not exist.

- [ ] **Step 3: Implement the setter**

`src/Abm.Pyro.Domain/IndexSetters/IPositionSetter.cs`:

```csharp
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Hl7.Fhir.ElementModel;

namespace Abm.Pyro.Domain.IndexSetters;

public interface IPositionSetter
{
  IList<IndexPosition> Set(ITypedElement typedElement, FhirResourceTypeId resourceType, int searchParameterId, string searchParameterName);
}
```

`src/Abm.Pyro.Domain/IndexSetters/PositionSetter.cs`:

```csharp
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace Abm.Pyro.Domain.IndexSetters;

public class PositionSetter(ILogger<PositionSetter> logger) : IPositionSetter
{
  private const decimal MinimumLatitude = -90m;
  private const decimal MaximumLatitude = 90m;
  private const decimal MinimumLongitude = -180m;
  private const decimal MaximumLongitude = 180m;
  private const int Wgs84Srid = 4326;

  private FhirResourceTypeId ResourceType;
  private int SearchParameterId;
  private string? SearchParameterName;

  public IList<IndexPosition> Set(ITypedElement typedElement, FhirResourceTypeId resourceType, int searchParameterId, string searchParameterName)
  {
    ResourceType = resourceType;
    SearchParameterId = searchParameterId;
    SearchParameterName = searchParameterName;

    if (typedElement is not IFhirValueProvider fhirValueProvider)
    {
      throw new NullReferenceException($"ITypedElement was expected to implement IFhirValueProvider for the SearchParameter entity with the database " +
                                       $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                       $"name of: {SearchParameterName}");
    }

    if (fhirValueProvider.FhirValue is null)
    {
      throw new NullReferenceException($"FhirValueProvider's FhirValue found to be null for the SearchParameter entity with the database " +
                                       $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                       $"name of: {SearchParameterName}");
    }

    return ProcessFhirDataType(fhirValueProvider.FhirValue);
  }

  private IList<IndexPosition> ProcessFhirDataType(Base fhirValue)
  {
    if (fhirValue is not Location.PositionComponent position)
    {
      throw new FormatException($"Unknown FhirType: {fhirValue.GetType().Name} for the SearchParameter entity with the database " +
                                $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                $"name of: {SearchParameterName}");
    }

    return SetPosition(position);
  }

  private IList<IndexPosition> SetPosition(Location.PositionComponent position)
  {
    // Both latitude and longitude are 1..1 in the specification, but a resource can still
    // arrive without them, so absence is handled rather than assumed away.
    if (position.Latitude is null || position.Longitude is null)
    {
      return Array.Empty<IndexPosition>();
    }

    decimal latitude = position.Latitude.Value;
    decimal longitude = position.Longitude.Value;

    // FHIR types these as plain decimals with no range constraint, so neither the parser nor
    // profile validation rejects an impossible coordinate. SQL Server's geography type does
    // reject it. Skipping the index row rather than throwing keeps one malformed Location from
    // failing its own create or update; the resource stores normally, it is simply not findable
    // through the 'near' search parameter.
    if (latitude < MinimumLatitude || latitude > MaximumLatitude ||
        longitude < MinimumLongitude || longitude > MaximumLongitude)
    {
      logger.LogWarning(
        "A {ResourceType} resource had a position outside the valid WGS84 range and was not indexed for the " +
        "search parameter: {SearchParameterName} (database key {SearchParameterStoreId}). " +
        "Latitude was {Latitude} (valid range -90 to 90) and longitude was {Longitude} (valid range -180 to 180).",
        ResourceType.GetCode(), SearchParameterName, SearchParameterId.ToString(), latitude, longitude);

      return Array.Empty<IndexPosition>();
    }

    // NetTopologySuite orders a Point as (X, Y), which is (longitude, latitude).
    var point = new Point((double)longitude, (double)latitude) { SRID = Wgs84Srid };

    return new List<IndexPosition>
    {
      new IndexPosition(
        indexPositionId: null,
        resourceStoreId: null,
        resourceStore: null,
        searchParameterStoreId: SearchParameterId,
        searchParameterStore: null,
        position: point)
    };
  }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/Abm.Pyro.Domain.Test/Abm.Pyro.Domain.Test.csproj --filter FullyQualifiedName~PositionSetterTest`
Expected: PASS, all tests including the out-of-range cases (Review Focus item 5 at the unit level; its end-to-end counterpart is in Task 9).

- [ ] **Step 5: Commit**

```bash
git add src/Abm.Pyro.Domain src/Abm.Pyro.Domain.Test
git commit -m "feat: add PositionSetter for Location.position indexing

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 5: Wire position indexing into the write path

**Files:**
- Modify: `src/Abm.Pyro.Domain/Indexing/IndexerOutcome.cs`
- Modify: `src/Abm.Pyro.Application/Indexing/Indexer.cs`
- Modify: `src/Abm.Pyro.Application/FhirHandler/FhirCreateHandler.cs:124`
- Modify: `src/Abm.Pyro.Application/FhirHandler/FhirUpdateHandler.cs:172`
- Modify: `src/Abm.Pyro.Application/FhirHandler/FhirPatchHandler.cs:165`
- Modify: `src/Abm.Pyro.Api/Program.cs`
- Create: `src/Abm.Pyro.Api.Test/Support/LocationBuilder.cs`
- Create: `src/Abm.Pyro.Api.Test/Search/NearSearchTests.cs` (first test only; extended in Task 9)

**Interfaces:**
- Consumes: `IPositionSetter` (Task 4), `IndexPosition` and `ResourceStore.IndexPositionList` (Task 2).
- Produces:
  - `IndexerOutcome.PositionIndexList` of type `List<IndexPosition>`; the `IndexerOutcome` constructor gains a trailing required parameter `List<IndexPosition> positionIndexList`.
  - `LocationBuilder.Build(string? id = null, string? name = null, decimal? latitude = null, decimal? longitude = null)` returning `Hl7.Fhir.Model.Location`.
  - `Abm.Pyro.Domain.FhirSupport.SearchParameterUrl.LocationNear` — the canonical URL constant.

- [ ] **Step 1: Add the canonical URL constant and extend IndexerOutcome**

Create `src/Abm.Pyro.Domain/FhirSupport/SearchParameterUrl.cs`:

```csharp
namespace Abm.Pyro.Domain.FhirSupport;

/// <summary>
/// Canonical URLs of search parameters that need bespoke handling.
/// </summary>
public static class SearchParameterUrl
{
  /// <summary>
  /// The Location 'near' search parameter: the only search parameter of type 'special' in the
  /// FHIR R4 search parameter set, and the only one this server supports.
  /// </summary>
  public const string LocationNear = "http://hl7.org/fhir/SearchParameter/Location-near";
}
```

In `src/Abm.Pyro.Domain/Indexing/IndexerOutcome.cs`, add the parameter and property:

```csharp
  public class IndexerOutcome(
    List<IndexString> stringIndexList,
    List<IndexReference> referenceIndexList,
    List<IndexDateTime> dateTimeIndexList,
    List<IndexQuantity> quantityIndexList,
    List<IndexToken> tokenIndexList,
    List<IndexUri> uriIndexList,
    List<IndexPosition> positionIndexList)
  {
    public List<IndexString> StringIndexList { get; private set; } = stringIndexList;
    public List<IndexReference> ReferenceIndexList { get; private set; } = referenceIndexList;
    public List<IndexDateTime> DateTimeIndexList { get; private set; } = dateTimeIndexList;
    public List<IndexQuantity> QuantityIndexList { get; private set; } = quantityIndexList;
    public List<IndexToken> TokenIndexList { get; private set; } = tokenIndexList;
    public List<IndexUri> UriIndexList { get; set; } = uriIndexList;
    public List<IndexPosition> PositionIndexList { get; set; } = positionIndexList;
  }
```

- [ ] **Step 2: Dispatch to the position setter in the Indexer**

In `src/Abm.Pyro.Application/Indexing/Indexer.cs`:

Add `IPositionSetter positionSetter,` to the primary constructor parameter list, after `IUriSetter uriSetter,`.

In `Process`, extend the `IndexerOutcome` construction:

```csharp
            IndexerOutcome = new IndexerOutcome(
                new List<IndexString>(),
                new List<IndexReference>(),
                new List<IndexDateTime>(),
                new List<IndexQuantity>(),
                new List<IndexToken>(),
                new List<IndexUri>(),
                new List<IndexPosition>());
```

Replace the `case SearchParamType.Special:` arm (currently at line 132) with:

```csharp
                case SearchParamType.Special:
                    if (searchParameter.Url is not null &&
                        searchParameter.Url.Equals(SearchParameterUrl.LocationNear, StringComparison.Ordinal))
                    {
                        GetPositionIndexList(resourceType, searchParameter, typedElement);
                        break;
                    }

                    logger.LogWarning("Encountered a search parameter of type: {SearchParamType} which is not supported by the server. The search parameter " +
                                      "had the code of : {SearchParameterCode} with a SearchParameterStore database primary key of {SearchParameterStoreId}. " +
                                      "The resource type being processed was of type : {ResourceType}",
                        SearchParamType.Special.ToString(),
                        searchParameter.Code,
                        searchParameter.SearchParameterStoreId.ToString(),
                        resourceType.ToString());
                    break;
```

Add the method alongside `GetUriIndexList`:

```csharp
        private void GetPositionIndexList(FhirResourceTypeId resourceType,
            SearchParameterProjection searchParameter,
            ITypedElement typedElement)
        {
            if (IndexerOutcome is null)
            {
                throw new NullReferenceException(nameof(IndexerOutcome));
            }

            if (!searchParameter.SearchParameterStoreId.HasValue)
            {
                throw new NullReferenceException(nameof(searchParameter.SearchParameterStoreId));
            }

            IList<IndexPosition> positionIndexList = positionSetter.Set(typedElement, resourceType, searchParameter.SearchParameterStoreId.Value, searchParameter.Code);
            IndexerOutcome.PositionIndexList.AddRange(positionIndexList);
        }
```

Add `using Abm.Pyro.Domain.FhirSupport;` to the file's using list if it is not already present.

If `SearchParameterProjection` has no `Url` property, use `searchParameter.Code == "near" && resourceType == FhirResourceTypeId.Location` instead, and note the substitution in the commit message.

- [ ] **Step 3: Pass the list through the three handlers**

In each of `FhirCreateHandler.cs`, `FhirUpdateHandler.cs` and `FhirPatchHandler.cs`, find the `new ResourceStore(...)` call that already passes `indexUriList: indexerOutcome.UriIndexList,` and add after the `rowVersion:` argument:

```csharp
            indexPositionList: indexerOutcome.PositionIndexList);
```

adjusting the preceding argument's trailing comma and closing parenthesis accordingly.

- [ ] **Step 4: Register the setter in DI**

In `src/Abm.Pyro.Api/Program.cs`, after `builder.Services.AddScoped<IUriSetter, UriSetter>();`:

```csharp
    builder.Services.AddScoped<IPositionSetter, PositionSetter>();
```

- [ ] **Step 5: Create the LocationBuilder**

`src/Abm.Pyro.Api.Test/Support/LocationBuilder.cs`:

```csharp
using Hl7.Fhir.Model;

namespace Abm.Pyro.Api.Test.Support;

public static class LocationBuilder
{
    public static Location Build(
        string? id = null,
        string? name = null,
        decimal? latitude = null,
        decimal? longitude = null)
    {
        var location = new Location
        {
            Id = id,
            Name = name ?? "TestLocation"
        };

        if (latitude.HasValue && longitude.HasValue)
        {
            location.Position = new Location.PositionComponent
            {
                LatitudeElement = new FhirDecimal(latitude.Value),
                LongitudeElement = new FhirDecimal(longitude.Value)
            };
        }

        return location;
    }
}
```

- [ ] **Step 6: Write the failing end-to-end indexing test**

`src/Abm.Pyro.Api.Test/Search/NearSearchTests.cs`. It covers Review Focus item 3 — a moved Location must not leave a stale index row. Position search is not wired up yet, so at this point only the create and update assertions can pass; the search assertions are added in Task 9.

```csharp
using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Search;

public class NearSearchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    // Sydney Opera House
    private const decimal SydneyLatitude = -33.8568m;
    private const decimal SydneyLongitude = 151.2153m;

    // Melbourne Flinders Street Station, roughly 714 km from Sydney
    private const decimal MelbourneLatitude = -37.8183m;
    private const decimal MelbourneLongitude = 144.9671m;

    [Fact]
    public async Task Create_LocationWithPosition_Succeeds()
    {
        Location? created = await FhirClient.CreateAsync(
            LocationBuilder.Build(name: "Opera House", latitude: SydneyLatitude, longitude: SydneyLongitude));

        Assert.NotNull(created);
        Assert.NotNull(created.Position);
    }

    /// <summary>
    /// A Location with an impossible coordinate must still store. FHIR places no range
    /// constraint on Location.position.latitude, so the server must not fail the write just
    /// because the value cannot be indexed as a geography point.
    /// </summary>
    [Fact]
    public async Task Create_LocationWithOutOfRangePosition_StillSucceeds()
    {
        Location? created = await FhirClient.CreateAsync(
            LocationBuilder.Build(name: "Impossible", latitude: 200m, longitude: 151.2153m));

        Assert.NotNull(created);
    }

    [Fact]
    public async Task Update_LocationPosition_Succeeds()
    {
        Location? created = await FhirClient.CreateAsync(
            LocationBuilder.Build(name: "Mover", latitude: SydneyLatitude, longitude: SydneyLongitude));
        Assert.NotNull(created);

        created.Position.LatitudeElement = new FhirDecimal(MelbourneLatitude);
        created.Position.LongitudeElement = new FhirDecimal(MelbourneLongitude);

        Location? updated = await FhirClient.UpdateAsync(created);

        Assert.NotNull(updated);
        Assert.Equal(MelbourneLatitude, updated.Position.Latitude);
    }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter FullyQualifiedName~NearSearchTests`
Expected: PASS, all three.

- [ ] **Step 8: Run the whole suite to confirm nothing regressed**

Run: `dotnet test src/Abm.Pyro.CI.slnf`
Expected: PASS. The `IndexerOutcome` and `ResourceStore` signature changes touch every write path, so the full suite is the check that matters here.

- [ ] **Step 9: Commit**

```bash
git add src/Abm.Pyro.Domain src/Abm.Pyro.Application src/Abm.Pyro.Api src/Abm.Pyro.Api.Test
git commit -m "feat: index Location.position on create, update and patch

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 6: Parse the near search parameter value

**Files:**
- Create: `src/Abm.Pyro.Domain/SearchQueryEntity/SearchQueryNearValue.cs`
- Create: `src/Abm.Pyro.Domain/SearchQueryEntity/SearchQueryNear.cs`
- Create: `src/Abm.Pyro.Domain.Test/SearchQueryEntity/SearchQueryNearTest.cs`

**Interfaces:**
- Consumes: `NearDistanceUnit`, `NearDistanceUnitSupport`, `LocationNearSettings` (Task 3).
- Produces:
  - `SearchQueryNearValue(bool isMissing, double latitude, double longitude, double distanceInMetres, NearDistanceUnit reportUnit)` with matching read-only properties `Latitude`, `Longitude`, `DistanceInMetres`, `ReportUnit`.
  - `SearchQueryNear(SearchParameterProjection searchParameter, FhirResourceTypeId resourceTypeContext, string rawValue, LocationNearSettings locationNearSettings)` with `List<SearchQueryNearValue> ValueList`.

- [ ] **Step 1: Write the failing parser tests**

`src/Abm.Pyro.Domain.Test/SearchQueryEntity/SearchQueryNearTest.cs`:

```csharp
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.SearchQueryEntity;
using Abm.Pyro.Domain.Support;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Domain.Test.SearchQueryEntity;

public class SearchQueryNearTest
{
    private static readonly LocationNearSettings Settings = new()
    {
        DefaultDistanceInMetres = 10_000,
        MaximumDistanceInMetres = 1_000_000,
        ReturnDistanceInSearchResults = true
    };

    private static SearchQueryNear CreateSut()
    {
        var searchParameter = new SearchParameterProjection
        {
            SearchParameterStoreId = 758,
            Code = "near",
            Type = SearchParamType.Special,
            Url = "http://hl7.org/fhir/SearchParameter/Location-near"
        };

        return new SearchQueryNear(searchParameter, FhirResourceTypeId.Location, "near=x", Settings);
    }

    [Fact]
    public async Task ParseValue_LatitudeLongitudeDistanceAndUnits_IsValid()
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue("-33.8568|151.2153|11.20|km");

        Assert.True(sut.IsValid);
        SearchQueryNearValue value = Assert.Single(sut.ValueList);
        Assert.Equal(-33.8568d, value.Latitude, 6);
        Assert.Equal(151.2153d, value.Longitude, 6);
        Assert.Equal(11200d, value.DistanceInMetres, 6);
        Assert.Equal(NearDistanceUnit.Kilometre, value.ReportUnit);
    }

    [Fact]
    public async Task ParseValue_UnitsOmitted_AssumesKilometres()
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue("-33.8568|151.2153|5");

        Assert.True(sut.IsValid);
        Assert.Equal(5000d, sut.ValueList[0].DistanceInMetres, 6);
        Assert.Equal(NearDistanceUnit.Kilometre, sut.ValueList[0].ReportUnit);
    }

    [Fact]
    public async Task ParseValue_EmptyUnitsSegment_AssumesKilometres()
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue("-33.8568|151.2153|5|");

        Assert.True(sut.IsValid);
        Assert.Equal(5000d, sut.ValueList[0].DistanceInMetres, 6);
    }

    [Fact]
    public async Task ParseValue_DistanceOmitted_UsesConfiguredDefault()
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue("-33.8568|151.2153");

        Assert.True(sut.IsValid);
        Assert.Equal(10_000d, sut.ValueList[0].DistanceInMetres, 6);
    }

    [Fact]
    public async Task ParseValue_Miles_ConvertsToMetres()
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue("-33.8568|151.2153|1|mi");

        Assert.True(sut.IsValid);
        Assert.Equal(1609.344d, sut.ValueList[0].DistanceInMetres, 6);
        Assert.Equal(NearDistanceUnit.Mile, sut.ValueList[0].ReportUnit);
    }

    [Fact]
    public async Task ParseValue_MultiplePositionsSeparatedByComma_ProducesTwoValues()
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue("-33.8568|151.2153|5|km,-37.8183|144.9671|5|km");

        Assert.True(sut.IsValid);
        Assert.Equal(2, sut.ValueList.Count);
        Assert.Equal(-33.8568d, sut.ValueList[0].Latitude, 6);
        Assert.Equal(-37.8183d, sut.ValueList[1].Latitude, 6);
    }

    /// <summary>
    /// Review Focus 1. A decimal written with a comma collides with the OR delimiter. It must
    /// be rejected, never silently split into two malformed terms and half-parsed.
    /// </summary>
    [Fact]
    public async Task ParseValue_CommaDecimalSeparator_IsInvalid()
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue("-33,8568|151,2153|5|km");

        Assert.False(sut.IsValid);
        Assert.NotNull(sut.InvalidMessage);
    }

    /// <summary>Review Focus 4. Zero and negative radii are meaningless and must be rejected.</summary>
    [Theory]
    [InlineData("-33.8568|151.2153|0|km")]
    [InlineData("-33.8568|151.2153|-5|km")]
    public async Task ParseValue_ZeroOrNegativeDistance_IsInvalid(string value)
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue(value);

        Assert.False(sut.IsValid);
    }

    [Theory]
    [InlineData("91|151.2153|5|km")]
    [InlineData("-91|151.2153|5|km")]
    [InlineData("-33.8568|181|5|km")]
    [InlineData("-33.8568|-181|5|km")]
    public async Task ParseValue_OutOfRangeCoordinate_IsInvalid(string value)
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue(value);

        Assert.False(sut.IsValid);
    }

    [Theory]
    [InlineData("-33.8568")]                       // too few segments
    [InlineData("-33.8568|151.2153|5|km|extra")]   // too many segments
    [InlineData("|151.2153|5|km")]                 // empty latitude
    [InlineData("-33.8568||5|km")]                 // empty longitude
    [InlineData("abc|151.2153|5|km")]              // non-numeric latitude
    [InlineData("-33.8568|151.2153|abc|km")]       // non-numeric distance
    public async Task ParseValue_MalformedValue_IsInvalid(string value)
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue(value);

        Assert.False(sut.IsValid);
    }

    [Theory]
    [InlineData("-33.8568|151.2153|5|cm")]
    [InlineData("-33.8568|151.2153|5|ft")]
    [InlineData("-33.8568|151.2153|5|nmi")]
    public async Task ParseValue_UnsupportedUnit_IsInvalid(string value)
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue(value);

        Assert.False(sut.IsValid);
        Assert.Contains("Supported units", sut.InvalidMessage);
    }

    [Fact]
    public async Task ParseValue_DistanceAboveConfiguredMaximum_IsInvalid()
    {
        SearchQueryNear sut = CreateSut();

        // 2000 km, above the 1,000,000 metre maximum
        await sut.ParseValue("-33.8568|151.2153|2000|km");

        Assert.False(sut.IsValid);
    }

    [Fact]
    public async Task ParseValue_MissingModifierTrue_ProducesMissingValue()
    {
        SearchQueryNear sut = CreateSut();
        sut.Modifier = SearchModifierCodeId.Missing;

        await sut.ParseValue("true");

        Assert.True(sut.IsValid);
        Assert.True(Assert.Single(sut.ValueList).IsMissing);
    }

    [Fact]
    public async Task ParseValue_MissingModifierNotBoolean_IsInvalid()
    {
        SearchQueryNear sut = CreateSut();
        sut.Modifier = SearchModifierCodeId.Missing;

        await sut.ParseValue("banana");

        Assert.False(sut.IsValid);
    }
}
```

If `SearchParameterProjection` cannot be constructed with an object initialiser, build it however `src/Abm.Pyro.Domain.Test/Factories/TestResourceFactory.cs` and the existing Domain tests build projections, and keep the field values above.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Abm.Pyro.Domain.Test/Abm.Pyro.Domain.Test.csproj --filter FullyQualifiedName~SearchQueryNearTest`
Expected: FAIL to compile — `SearchQueryNear` does not exist.

- [ ] **Step 3: Implement the value type**

`src/Abm.Pyro.Domain/SearchQueryEntity/SearchQueryNearValue.cs`:

```csharp
using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public class SearchQueryNearValue(
  bool isMissing,
  double latitude,
  double longitude,
  double distanceInMetres,
  NearDistanceUnit reportUnit)
  : SearchQueryValueBase(isMissing)
{
  public double Latitude { get; } = latitude;
  public double Longitude { get; } = longitude;

  /// <summary>
  /// The search radius in metres. Metres is canonical below the parser because SQL Server's
  /// STDistance returns metres.
  /// </summary>
  public double DistanceInMetres { get; } = distanceInMetres;

  /// <summary>
  /// The unit the client expressed the distance in, retained only so a computed distance can be
  /// reported back in the same unit rather than always in kilometres.
  /// </summary>
  public NearDistanceUnit ReportUnit { get; } = reportUnit;
}
```

- [ ] **Step 4: Implement the parser**

`src/Abm.Pyro.Domain/SearchQueryEntity/SearchQueryNear.cs`:

```csharp
using System.Globalization;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Support;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Domain.SearchQueryEntity;

/// <summary>
/// Parses the FHIR R4 Location 'near' search parameter, whose value is
/// [latitude]|[longitude]|[distance]|[units], with the distance and units optional.
/// See https://hl7.org/fhir/R4/location.html#positional
/// </summary>
public class SearchQueryNear(
  SearchParameterProjection searchParameter,
  FhirResourceTypeId resourceTypeContext,
  string rawValue,
  LocationNearSettings locationNearSettings)
  : SearchQueryBase(searchParameter, resourceTypeContext, rawValue)
{
  private const char VerticalBarDelimiter = '|';
  private const double MinimumLatitude = -90d;
  private const double MaximumLatitude = 90d;
  private const double MinimumLongitude = -180d;
  private const double MaximumLongitude = 180d;

  public List<SearchQueryNearValue> ValueList { get; set; } = new();

  public override object CloneDeep()
  {
    var clone = new SearchQueryNear(SearchParameter, ResourceTypeContext, RawValue, locationNearSettings);
    base.CloneDeep(clone);
    clone.ValueList = new List<SearchQueryNearValue>();
    clone.ValueList.AddRange(ValueList);
    return clone;
  }

  public override Task ParseValue(string values)
  {
    IsValid = true;
    ValueList.Clear();

    foreach (string value in values.Split(OrDelimiter))
    {
      if (Modifier.HasValue && Modifier == SearchModifierCodeId.Missing)
      {
        if (!ParseMissingValue(value))
        {
          break;
        }

        continue;
      }

      if (!ParsePositionValue(value))
      {
        break;
      }
    }

    return Task.CompletedTask;
  }

  private bool ParseMissingValue(string value)
  {
    bool? isMissing = SearchQueryValueBase.ParseModifierEqualToMissing(value);
    if (isMissing.HasValue)
    {
      ValueList.Add(new SearchQueryNearValue(isMissing.Value, 0d, 0d, 0d, NearDistanceUnit.Kilometre));
      return true;
    }

    InvalidMessage = $"Found the {SearchModifierCodeId.Missing.GetCode()} Modifier yet its value was expected to be true or false yet found '{value}'. ";
    IsValid = false;
    return false;
  }

  private bool ParsePositionValue(string value)
  {
    // The value is [latitude]|[longitude]|[distance]|[units]. Note that the order is latitude
    // then longitude. The worked example in the FHIR R4 specification at
    // https://hl7.org/fhir/R4/location.html#positional has the two transposed; it is a known
    // erratum and the normative parameter description is followed here.
    string[] split = value.Trim().Split(VerticalBarDelimiter);

    if (split.Length is < 2 or > 4)
    {
      InvalidMessage = $"The '{SearchParameter.Code}' search parameter value '{value}' was expected to be of the form " +
                       $"[latitude]|[longitude]|[distance]|[units] where the distance and units are optional, " +
                       $"yet {split.Length.ToString()} vertical-bar separated segments were found. ";
      IsValid = false;
      return false;
    }

    if (!TryParseCoordinate(split[0], MinimumLatitude, MaximumLatitude, "latitude", value, out double latitude))
    {
      return false;
    }

    if (!TryParseCoordinate(split[1], MinimumLongitude, MaximumLongitude, "longitude", value, out double longitude))
    {
      return false;
    }

    string? unitCode = split.Length == 4 ? split[3] : null;
    if (!NearDistanceUnitSupport.TryParse(unitCode, out NearDistanceUnit unit))
    {
      InvalidMessage = $"The '{SearchParameter.Code}' search parameter value '{value}' had an unsupported distance unit of '{unitCode}'. " +
                       $"{NearDistanceUnitSupport.SupportedUnitsMessage} ";
      IsValid = false;
      return false;
    }

    string distanceAsString = split.Length >= 3 ? split[2].Trim() : string.Empty;
    double distanceInMetres;

    if (distanceAsString.Length == 0)
    {
      // FHIR R4 leaves the radius to the server's discretion when the distance is omitted.
      distanceInMetres = locationNearSettings.DefaultDistanceInMetres;
    }
    else
    {
      if (!decimal.TryParse(distanceAsString, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal distance))
      {
        InvalidMessage = $"The '{SearchParameter.Code}' search parameter value '{value}' had a distance of '{distanceAsString}' which could not be parsed as a number. ";
        IsValid = false;
        return false;
      }

      if (distance <= 0m)
      {
        InvalidMessage = $"The '{SearchParameter.Code}' search parameter value '{value}' had a distance of '{distanceAsString}' which must be greater than zero. ";
        IsValid = false;
        return false;
      }

      distanceInMetres = NearDistanceUnitSupport.ToMetres(distance, unit);

      if (distanceInMetres > locationNearSettings.MaximumDistanceInMetres)
      {
        InvalidMessage = $"The '{SearchParameter.Code}' search parameter value '{value}' requested a distance of {distanceInMetres.ToString(CultureInfo.InvariantCulture)} metres " +
                         $"which is greater than the maximum this server supports of {locationNearSettings.MaximumDistanceInMetres.ToString(CultureInfo.InvariantCulture)} metres. ";
        IsValid = false;
        return false;
      }
    }

    ValueList.Add(new SearchQueryNearValue(false, latitude, longitude, distanceInMetres, unit));
    return true;
  }

  private bool TryParseCoordinate(string segment, double minimum, double maximum, string coordinateName, string value, out double coordinate)
  {
    if (!double.TryParse(segment.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out coordinate))
    {
      InvalidMessage = $"The '{SearchParameter.Code}' search parameter value '{value}' had a {coordinateName} of '{segment}' which could not be parsed as a number. " +
                       $"Note that the decimal separator must be a full stop, because a comma separates multiple positions. ";
      IsValid = false;
      return false;
    }

    if (coordinate < minimum || coordinate > maximum)
    {
      InvalidMessage = $"The '{SearchParameter.Code}' search parameter value '{value}' had a {coordinateName} of '{segment}' which is outside the valid range of " +
                       $"{minimum.ToString(CultureInfo.InvariantCulture)} to {maximum.ToString(CultureInfo.InvariantCulture)}. ";
      IsValid = false;
      return false;
    }

    return true;
  }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/Abm.Pyro.Domain.Test/Abm.Pyro.Domain.Test.csproj --filter FullyQualifiedName~SearchQueryNearTest`
Expected: PASS, all tests.

- [ ] **Step 6: Commit**

```bash
git add src/Abm.Pyro.Domain src/Abm.Pyro.Domain.Test
git commit -m "feat: parse the Location near search parameter value

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 7: Dispatch near in the SearchQueryFactory

**Files:**
- Modify: `src/Abm.Pyro.Domain/SearchQuery/SearchQueryFactory.cs:107`

**Interfaces:**
- Consumes: `SearchQueryNear` (Task 6), `LocationNearSettings` (Task 3), `SearchParameterUrl.LocationNear` (Task 5).
- Produces: nothing new; `SearchQueryFactory`'s primary constructor gains `IOptions<LocationNearSettings> locationNearSettingsOptions`.

- [ ] **Step 1: Add the settings dependency**

In `src/Abm.Pyro.Domain/SearchQuery/SearchQueryFactory.cs`, add to the primary constructor:

```csharp
public class SearchQueryFactory(
  IFhirUriFactory fhirUriFactory,
  IFhirResourceTypeSupport fhirResourceTypeSupport,
  IFhirDateTimeFactory fhirDateTimeFactory,
  ISearchParameterCache searchParameterCache,
  IOptions<LocationNearSettings> locationNearSettingsOptions)
  : ISearchQueryFactory
```

and the usings:

```csharp
using Abm.Pyro.Domain.Configuration;
using Microsoft.Extensions.Options;
```

- [ ] **Step 2: Replace the Special arm**

In `InitializeSearchQueryEntity`, replace:

```csharp
      SearchParamType.Special => new SearchQueryNumber(searchParameter, ResourceContext, RawValue),
```

with:

```csharp
      SearchParamType.Special => InitializeSpecialSearchQueryEntity(searchParameter, ResourceContext, RawValue),
```

and add the method to the class:

```csharp
  /// <summary>
  /// Location 'near' is the only search parameter of type 'special' in FHIR R4. Any other
  /// special parameter is marked invalid rather than mapped onto a type it does not match,
  /// so the client receives a 400 explaining the parameter is unsupported instead of a 500.
  /// </summary>
  private SearchQueryBase InitializeSpecialSearchQueryEntity(SearchParameterProjection searchParameter, FhirResourceTypeId resourceContext, string rawValue)
  {
    var searchQueryNear = new SearchQueryNear(searchParameter, resourceContext, rawValue, locationNearSettingsOptions.Value);

    if (searchParameter.Url is null ||
        !searchParameter.Url.Equals(SearchParameterUrl.LocationNear, StringComparison.Ordinal))
    {
      searchQueryNear.IsValid = false;
      searchQueryNear.InvalidMessage =
        $"The search parameter '{searchParameter.Code}' is of type '{SearchParamType.Special.GetCode()}' which this server " +
        $"only supports for the Location 'near' search parameter. ";
    }

    return searchQueryNear;
  }
```

Add `using Abm.Pyro.Domain.FhirSupport;` if not already present.

If `SearchParameterProjection.Url` is typed as `Uri` rather than `string`, compare with
`searchParameter.Url.OriginalString.Equals(SearchParameterUrl.LocationNear, StringComparison.Ordinal)`.

**This branch carries no test, deliberately.** `Location-near` is the only `special` search
parameter in the R4 seed, so the "some other special parameter" path cannot be reached without
seeding a fictitious search parameter. Testing it would mean constructing `SearchQueryFactory`
with four mocked collaborators to exercise a branch that no real request can reach. It exists so
that a future R4 extension, or a custom SearchParameter loaded into the server, degrades to a 400
rather than the 500 it would produce today. Verify it by reading, not by test.

- [ ] **Step 3: Verify the solution builds**

Run: `dotnet build src/Abm.Pyro.CI.slnf`
Expected: succeeds. If `SearchQueryFactory` is constructed directly anywhere in tests, those call sites need the new argument — fix them by passing
`Options.Create(new LocationNearSettings())`.

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test src/Abm.Pyro.CI.slnf`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Abm.Pyro.Domain src/Abm.Pyro.Domain.Test src/Abm.Pyro.Application.Test
git commit -m "feat: dispatch Location near to SearchQueryNear in the search query factory

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 8: Position predicate factory

**Files:**
- Create: `src/Abm.Pyro.Repository/Predicates/IIndexPositionPredicateFactory.cs`
- Create: `src/Abm.Pyro.Repository/Predicates/IndexPositionPredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/IResourceStorePredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/ResourceStorePredicateFactory.cs`
- Modify: `src/Abm.Pyro.Repository/Predicates/SearchSearchPredicateFactory.cs:50`
- Modify: `src/Abm.Pyro.Api/Program.cs`

**Interfaces:**
- Consumes: `SearchQueryNear` (Task 6), `IndexPosition` (Task 2).
- Produces:
  - `IIndexPositionPredicateFactory.PositionIndex(SearchQueryNear searchQueryNear)` returning `List<Expression<Func<IndexPosition, bool>>>`.
  - `IResourceStorePredicateFactory.PositionIndex(SearchQueryBase searchQueryBase)` returning `List<Expression<Func<IndexPosition, bool>>>`.

- [ ] **Step 1: Create the predicate factory interface**

`src/Abm.Pyro.Repository/Predicates/IIndexPositionPredicateFactory.cs`:

```csharp
using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;

namespace Abm.Pyro.Repository.Predicates;

public interface IIndexPositionPredicateFactory
{
  List<Expression<Func<IndexPosition, bool>>> PositionIndex(SearchQueryNear searchQueryNear);
}
```

- [ ] **Step 2: Implement the predicate factory**

`src/Abm.Pyro.Repository/Predicates/IndexPositionPredicateFactory.cs`:

```csharp
using System.Linq.Expressions;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
using NetTopologySuite.Geometries;

namespace Abm.Pyro.Repository.Predicates;

public class IndexPositionPredicateFactory : IIndexPositionPredicateFactory
{
  private const int Wgs84Srid = 4326;

  public List<Expression<Func<IndexPosition, bool>>> PositionIndex(SearchQueryNear searchQueryNear)
  {
    var resultList = new List<Expression<Func<IndexPosition, bool>>>();

    foreach (SearchQueryNearValue nearValue in searchQueryNear.ValueList)
    {
      if (!searchQueryNear.SearchParameter.SearchParameterStoreId.HasValue)
      {
        throw new ArgumentNullException(nameof(searchQueryNear.SearchParameter.SearchParameterStoreId));
      }

      int searchParameterId = searchQueryNear.SearchParameter.SearchParameterStoreId.Value;

      var predicate = LinqKit.PredicateBuilder.New<IndexPosition>(true);

      if (!searchQueryNear.Modifier.HasValue)
      {
        predicate = predicate.And(IsSearchParameterId(searchParameterId));
        predicate = predicate.And(WithinDistanceOf(nearValue));
        resultList.Add(predicate);
        continue;
      }

      var arrayOfSupportedModifiers = FhirSearchQuerySupport.GetModifiersForSearchType(searchQueryNear.SearchParameter.Type);
      if (!arrayOfSupportedModifiers.Contains(searchQueryNear.Modifier.Value))
      {
        throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryNear.Modifier.Value.GetCode()} is not supported for search parameter types of {searchQueryNear.SearchParameter.Type.GetCode()}.");
      }

      switch (searchQueryNear.Modifier.Value)
      {
        case SearchModifierCodeId.Missing:
          predicate = predicate.And(IsNotSearchParameterId(searchParameterId));
          resultList.Add(predicate);
          break;
        default:
          throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryNear.Modifier.Value.GetCode()} has been added to the supported list for {searchQueryNear.SearchParameter.Type.GetCode()} search parameter queries and yet no database predicate has been provided.");
      }
    }

    return resultList;
  }

  /// <summary>
  /// Translates to STDistance(Position, @point) &lt;= @metres, which is the form SQL Server can
  /// serve from a spatial index. STDistance returns metres, and the search radius is already in
  /// metres by the time it reaches here.
  /// </summary>
  private Expression<Func<IndexPosition, bool>> WithinDistanceOf(SearchQueryNearValue nearValue)
  {
    // NetTopologySuite orders a Point as (X, Y), which is (longitude, latitude).
    var targetPoint = new Point(nearValue.Longitude, nearValue.Latitude) { SRID = Wgs84Srid };
    double distanceInMetres = nearValue.DistanceInMetres;

    return x => x.Position.Distance(targetPoint) <= distanceInMetres;
  }

  private Expression<Func<IndexPosition, bool>> IsSearchParameterId(int searchParameterId)
  {
    return x => x.SearchParameterStoreId == searchParameterId;
  }

  private Expression<Func<IndexPosition, bool>> IsNotSearchParameterId(int searchParameterId)
  {
    return x => x.SearchParameterStoreId != searchParameterId;
  }
}
```

- [ ] **Step 3: Add PositionIndex to the ResourceStore predicate factory**

In `src/Abm.Pyro.Repository/Predicates/IResourceStorePredicateFactory.cs`, add after `UriIndex`:

```csharp
  List<Expression<Func<IndexPosition, bool>>> PositionIndex(SearchQueryBase searchQueryBase);
```

In `src/Abm.Pyro.Repository/Predicates/ResourceStorePredicateFactory.cs`, add `IIndexPositionPredicateFactory indexPositionPredicateFactory` to the primary constructor parameter list, and add the method after `UriIndex`, matching the existing shape exactly:

```csharp
  public List<Expression<Func<IndexPosition, bool>>> PositionIndex(SearchQueryBase searchQueryBase)
  {
    if (searchQueryBase is SearchQueryNear searchQueryNear)
    {
      return indexPositionPredicateFactory.PositionIndex(searchQueryNear);
    }

    throw new InvalidCastException($"Unable to cast a {nameof(SearchQueryBase)} of type {searchQueryBase.GetType().Name} to a {nameof(SearchQueryNear)}");
  }
```

- [ ] **Step 4: Replace the throwing Special arm in the search predicate factory**

In `src/Abm.Pyro.Repository/Predicates/SearchSearchPredicateFactory.cs`, replace:

```csharp
        case SearchParamType.Special:
          throw new FhirFatalException(System.Net.HttpStatusCode.InternalServerError, new string[] { $"Attempt to search with a SearchParameter of type: {SearchParamType.Special.GetCode()} which is not supported by this server." });
```

with:

```csharp
        case SearchParamType.Special:
          resourceStorePredicateFactory.PositionIndex(searchQuery).ForEach(x => predicateInner = predicateInner.Or(y => y.IndexPositionList.Any(x.Compile())));
          break;
```

This is deliberately identical in shape to the seven arms around it. The `.Compile()` call is a LinqKit marker expanded by `AsExpandable()` at query time, exactly as in the other arms; it never executes in memory.

- [ ] **Step 5: Register in DI**

In `src/Abm.Pyro.Api/Program.cs`, after `builder.Services.AddSingleton<IIndexUriPredicateFactory, IndexUriPredicateFactory>();`:

```csharp
    builder.Services.AddSingleton<IIndexPositionPredicateFactory, IndexPositionPredicateFactory>();
```

- [ ] **Step 6: Verify the build and full suite**

Run: `dotnet build src/Abm.Pyro.CI.slnf`
Expected: succeeds.

Run: `dotnet test src/Abm.Pyro.CI.slnf`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Abm.Pyro.Repository src/Abm.Pyro.Api
git commit -m "feat: build the near search predicate from the position index

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 9: Integration tests for near filtering

**Files:**
- Modify: `src/Abm.Pyro.Api.Test/Search/NearSearchTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 2–8.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Add the filtering tests**

Append these to `src/Abm.Pyro.Api.Test/Search/NearSearchTests.cs`, inside the existing class. They cover Review Focus items 2, 3 and 5 end to end.

```csharp
    [Fact]
    public async Task Search_NearWithinRadius_ReturnsTheLocation()
    {
        await CreateLocationAsync("Opera House", SydneyLatitude, SydneyLongitude);

        Bundle? bundle = await FhirClient.SearchAsync<Location>(
            new[] { "near=-33.8568|151.2153|5|km" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_NearOutsideRadius_ReturnsEmptyBundle()
    {
        await CreateLocationAsync("Flinders Street", MelbourneLatitude, MelbourneLongitude);

        Bundle? bundle = await FhirClient.SearchAsync<Location>(
            new[] { "near=-33.8568|151.2153|5|km" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_Near_OnlyReturnsLocationsInsideTheRadius()
    {
        await CreateLocationAsync("Opera House", SydneyLatitude, SydneyLongitude);
        await CreateLocationAsync("Flinders Street", MelbourneLatitude, MelbourneLongitude);

        Bundle? bundle = await FhirClient.SearchAsync<Location>(
            new[] { "near=-33.8568|151.2153|5|km" });

        Assert.NotNull(bundle);
        Bundle.EntryComponent entry = Assert.Single(bundle.Entry);
        Assert.Equal("Opera House", ((Location)entry.Resource).Name);
    }

    [Fact]
    public async Task Search_NearWithDistanceOmitted_UsesTheServerDefaultOfTenKilometres()
    {
        await CreateLocationAsync("Opera House", SydneyLatitude, SydneyLongitude);
        await CreateLocationAsync("Flinders Street", MelbourneLatitude, MelbourneLongitude);

        Bundle? bundle = await FhirClient.SearchAsync<Location>(
            new[] { "near=-33.8568|151.2153" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Theory]
    [InlineData("near=-33.8568|151.2153|5|km")]
    [InlineData("near=-33.8568|151.2153|5000|m")]
    [InlineData("near=-33.8568|151.2153|3.10686|mi")]
    [InlineData("near=-33.8568|151.2153|3.10686|[mi_i]")]
    public async Task Search_NearInDifferentUnits_ReturnsTheSameLocation(string query)
    {
        await CreateLocationAsync("Opera House", SydneyLatitude, SydneyLongitude);

        Bundle? bundle = await FhirClient.SearchAsync<Location>(new[] { query });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_NearWithMultiplePositions_ReturnsLocationsNearEither()
    {
        await CreateLocationAsync("Opera House", SydneyLatitude, SydneyLongitude);
        await CreateLocationAsync("Flinders Street", MelbourneLatitude, MelbourneLongitude);

        Bundle? bundle = await FhirClient.SearchAsync<Location>(
            new[] { "near=-33.8568|151.2153|5|km,-37.8183|144.9671|5|km" });

        Assert.NotNull(bundle);
        Assert.Equal(2, bundle.Entry.Count);
    }

    /// <summary>
    /// Review Focus 2. A Location just west of the antimeridian must be found by a search just
    /// east of it. This is the case a latitude/longitude bounding box gets wrong; STDistance on
    /// a geography column gets it right.
    /// </summary>
    [Fact]
    public async Task Search_NearAcrossTheAntimeridian_ReturnsTheLocation()
    {
        await CreateLocationAsync("West of the line", 0m, -179.95m);

        Bundle? bundle = await FhirClient.SearchAsync<Location>(
            new[] { "near=0|179.95|20|km" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    /// <summary>
    /// Review Focus 3. Moving a Location must replace its position index, not add to it.
    /// </summary>
    [Fact]
    public async Task Search_AfterMovingALocation_DoesNotFindItAtTheOldPosition()
    {
        Location? created = await FhirClient.CreateAsync(
            LocationBuilder.Build(name: "Mover", latitude: SydneyLatitude, longitude: SydneyLongitude));
        Assert.NotNull(created);

        created.Position.LatitudeElement = new FhirDecimal(MelbourneLatitude);
        created.Position.LongitudeElement = new FhirDecimal(MelbourneLongitude);
        await FhirClient.UpdateAsync(created);

        Bundle? atOldPosition = await FhirClient.SearchAsync<Location>(
            new[] { "near=-33.8568|151.2153|5|km" });
        Bundle? atNewPosition = await FhirClient.SearchAsync<Location>(
            new[] { "near=-37.8183|144.9671|5|km" });

        Assert.NotNull(atOldPosition);
        Assert.Empty(atOldPosition.Entry);
        Assert.NotNull(atNewPosition);
        Assert.Single(atNewPosition.Entry);
    }

    /// <summary>
    /// Review Focus 5, end to end. An impossible coordinate stores but is not findable.
    /// </summary>
    [Fact]
    public async Task Search_LocationWithOutOfRangePosition_IsNotFoundButDidStore()
    {
        Location? created = await FhirClient.CreateAsync(
            LocationBuilder.Build(name: "Impossible", latitude: 200m, longitude: 151.2153m));
        Assert.NotNull(created);

        Location? readBack = await FhirClient.ReadAsync<Location>($"Location/{created.Id}");
        Assert.NotNull(readBack);

        Bundle? bundle = await FhirClient.SearchAsync<Location>(
            new[] { "near=-33.8568|151.2153|5000|km" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_NearMissingTrue_ReturnsLocationsWithoutAPosition()
    {
        await CreateLocationAsync("Opera House", SydneyLatitude, SydneyLongitude);
        Location? noPosition = await FhirClient.CreateAsync(LocationBuilder.Build(name: "Nowhere"));
        Assert.NotNull(noPosition);

        Bundle? bundle = await FhirClient.SearchAsync<Location>(new[] { "near:missing=true" });

        Assert.NotNull(bundle);
        Bundle.EntryComponent entry = Assert.Single(bundle.Entry);
        Assert.Equal("Nowhere", ((Location)entry.Resource).Name);
    }

    [Theory]
    [InlineData("near=-33,8568|151,2153|5|km")]      // Review Focus 1: comma decimal separator
    [InlineData("near=-33.8568|151.2153|0|km")]      // Review Focus 4: zero distance
    [InlineData("near=-33.8568|151.2153|-5|km")]     // Review Focus 4: negative distance
    [InlineData("near=-33.8568")]                    // too few segments
    [InlineData("near=-33.8568|151.2153|5|km|extra")]// too many segments
    [InlineData("near=91|151.2153|5|km")]            // latitude out of range
    [InlineData("near=-33.8568|181|5|km")]           // longitude out of range
    [InlineData("near=-33.8568|151.2153|5|cm")]      // unsupported unit
    [InlineData("near=-33.8568|151.2153|5|ft")]      // unsupported unit
    [InlineData("near=-33.8568|151.2153|2000|km")]   // above the configured maximum
    public async Task Search_MalformedNearValue_ReturnsBadRequest(string query)
    {
        await CreateLocationAsync("Opera House", SydneyLatitude, SydneyLongitude);

        var exception = await Assert.ThrowsAsync<Hl7.Fhir.Rest.FhirOperationException>(
            () => FhirClient.SearchAsync<Location>(new[] { query }));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, exception.Status);
    }

    private async Task CreateLocationAsync(string name, decimal latitude, decimal longitude)
    {
        Location? location = await FhirClient.CreateAsync(
            LocationBuilder.Build(name: name, latitude: latitude, longitude: longitude));
        Assert.NotNull(location);
    }
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter FullyQualifiedName~NearSearchTests`
Expected: PASS.

If `Search_NearAcrossTheAntimeridian_ReturnsTheLocation` fails, the coordinates are being transposed somewhere; check `PositionSetter` and `IndexPositionPredicateFactory` both put longitude in X.

If the malformed-value tests return 500 rather than 400, the invalid parse result is not reaching the standard invalid-query response path; trace `SearchQueryNear.IsValid` through `SearchQueryService` into `SearchQueryServiceOutcome.InvalidSearchQueryList`.

- [ ] **Step 3: Commit**

```bash
git add src/Abm.Pyro.Api.Test
git commit -m "test: integration tests for Location near filtering

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 10: Phase 2 distance computation

**Files:**
- Create: `src/Abm.Pyro.Domain/Query/NearDistance.cs`
- Create: `src/Abm.Pyro.Repository/Query/INearDistanceQuery.cs`
- Create: `src/Abm.Pyro.Repository/Query/NearDistanceQuery.cs`
- Modify: `src/Abm.Pyro.Domain/Query/ResourceStoreSearchOutcome.cs`
- Modify: `src/Abm.Pyro.Repository/Query/ResourceStoreSearch.cs`
- Modify: `src/Abm.Pyro.Api/Program.cs`

**Interfaces:**
- Consumes: `SearchQueryNear` (Task 6), `IndexPosition` (Task 2), `LocationNearSettings` (Task 3).
- Produces:
  - `record NearDistance(double Metres, NearDistanceUnit ReportUnit)` in `Abm.Pyro.Domain.Query`.
  - `INearDistanceQuery.GetNearestDistances(IReadOnlyCollection<int> resourceStoreIdList, SearchQueryNear searchQueryNear)` returning `Task<IReadOnlyDictionary<int, NearDistance>>`.
  - `ResourceStoreSearchOutcome.NearDistanceByResourceStoreId` of type `IReadOnlyDictionary<int, NearDistance>?`; the constructor gains a trailing optional parameter `IReadOnlyDictionary<int, NearDistance>? nearDistanceByResourceStoreId = null`.

- [ ] **Step 1: Create the NearDistance record**

`src/Abm.Pyro.Domain/Query/NearDistance.cs`:

```csharp
using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Domain.Query;

/// <summary>
/// How far a matched Location is from the point a client searched near, and the unit the client
/// expressed their search in, so the result can be reported back in the same unit.
/// </summary>
public record NearDistance(double Metres, NearDistanceUnit ReportUnit);
```

- [ ] **Step 2: Add the map to ResourceStoreSearchOutcome**

In `src/Abm.Pyro.Domain/Query/ResourceStoreSearchOutcome.cs`:

```csharp
public class ResourceStoreSearchOutcome(
  int searchTotal,
  int pageRequested,
  int pagesTotal,
  List<ResourceStore> resourceStoreList,
  List<ResourceStore> includedResourceStoreList,
  IReadOnlyDictionary<int, NearDistance>? nearDistanceByResourceStoreId = null)
{
  public int SearchTotal { get; } = searchTotal;
  public int PageRequested { get; } = pageRequested;
  public int PagesTotal { get; } = pagesTotal;
  public  List<ResourceStore> ResourceStoreList { get; } = resourceStoreList;
  public  List<ResourceStore> IncludedResourceStoreList { get; } = includedResourceStoreList;

  /// <summary>
  /// For a Location 'near' search, each matched resource's distance from the nearest searched
  /// point. Null when the search had no near term, or when
  /// LocationNearSettings.ReturnDistanceInSearchResults is false.
  /// </summary>
  public IReadOnlyDictionary<int, NearDistance>? NearDistanceByResourceStoreId { get; } = nearDistanceByResourceStoreId;
  ...
```

Leave `EmptyResult()` unchanged; the new parameter is optional.

- [ ] **Step 3: Implement the phase 2 query**

`src/Abm.Pyro.Repository/Query/INearDistanceQuery.cs`:

```csharp
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQueryEntity;

namespace Abm.Pyro.Repository.Query;

public interface INearDistanceQuery
{
  Task<IReadOnlyDictionary<int, NearDistance>> GetNearestDistances(
    IReadOnlyCollection<int> resourceStoreIdList,
    SearchQueryNear searchQueryNear);
}
```

`src/Abm.Pyro.Repository/Query/NearDistanceQuery.cs`:

```csharp
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQueryEntity;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Abm.Pyro.Repository.Query;

/// <summary>
/// Phase two of a Location 'near' search. The filtering predicate is a correlated EXISTS and so
/// cannot surface a computed distance; this runs afterwards over the single page of matched
/// resources and computes each one's distance with the same STDistance call that filtered it, so
/// the filter and the reported distance can never disagree at the radius boundary.
/// </summary>
public class NearDistanceQuery(PyroDbContext context) : INearDistanceQuery
{
  private const int Wgs84Srid = 4326;

  public async Task<IReadOnlyDictionary<int, NearDistance>> GetNearestDistances(
    IReadOnlyCollection<int> resourceStoreIdList,
    SearchQueryNear searchQueryNear)
  {
    var result = new Dictionary<int, NearDistance>();

    if (resourceStoreIdList.Count == 0 || !searchQueryNear.SearchParameter.SearchParameterStoreId.HasValue)
    {
      return result;
    }

    int searchParameterId = searchQueryNear.SearchParameter.SearchParameterStoreId.Value;

    // One query per searched position. A single query taking the minimum across N points would
    // need Math.Min inside the expression tree, which does not translate reliably to T-SQL, and
    // N is almost always one.
    foreach (SearchQueryNearValue nearValue in searchQueryNear.ValueList)
    {
      // A ':missing' term names no coordinate to measure from, so there is nothing to compute.
      if (nearValue.IsMissing)
      {
        continue;
      }

      // NetTopologySuite orders a Point as (X, Y), which is (longitude, latitude).
      var targetPoint = new Point(nearValue.Longitude, nearValue.Latitude) { SRID = Wgs84Srid };

      var distanceList = await context.Set<IndexPosition>()
        .Where(x => x.SearchParameterStoreId == searchParameterId &&
                    x.ResourceStoreId != null &&
                    resourceStoreIdList.Contains(x.ResourceStoreId.Value))
        .Select(x => new
        {
          ResourceStoreId = x.ResourceStoreId!.Value,
          Metres = x.Position.Distance(targetPoint)
        })
        .ToListAsync();

      foreach (var distance in distanceList)
      {
        if (!result.TryGetValue(distance.ResourceStoreId, out NearDistance? existing) ||
            distance.Metres < existing.Metres)
        {
          result[distance.ResourceStoreId] = new NearDistance(distance.Metres, nearValue.ReportUnit);
        }
      }
    }

    return result;
  }
}
```

- [ ] **Step 4: Run phase 2 from ResourceStoreSearch**

In `src/Abm.Pyro.Repository/Query/ResourceStoreSearch.cs`, add to the primary constructor:

```csharp
    INearDistanceQuery nearDistanceQuery,
    IOptions<LocationNearSettings> locationNearSettingsOptions,
```

and the usings:

```csharp
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.SearchQueryEntity;
using Microsoft.Extensions.Options;
```

In `GetSearch`, after `includedResourceStoreList` is built and before the `return`:

```csharp
            IReadOnlyDictionary<int, NearDistance>? nearDistanceByResourceStoreId =
                await GetNearDistancesIfRequired(searchQueryServiceOutcome, targetResourceStoreList);

            return new ResourceStoreSearchOutcome(
                searchTotal: totalRecordCount,
                pageRequested: pageRequired,
                pagesTotal: paginationSupport.CalculateTotalPages(searchQueryServiceOutcome.CountRequested, totalRecordCount),
                resourceStoreList: targetResourceStoreList,
                includedResourceStoreList: includedResourceStoreList,
                nearDistanceByResourceStoreId: nearDistanceByResourceStoreId);
```

and add the private method:

```csharp
    /// <summary>
    /// Computes each matched Location's distance from the searched point, but only when the
    /// server is configured to report it and the query actually contained a near term. This runs
    /// against the materialised page, never the whole result set.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, NearDistance>?> GetNearDistancesIfRequired(
        SearchQueryServiceOutcome searchQueryServiceOutcome,
        List<ResourceStore> targetResourceStoreList)
    {
        if (!locationNearSettingsOptions.Value.ReturnDistanceInSearchResults)
        {
            return null;
        }

        SearchQueryNear? searchQueryNear = searchQueryServiceOutcome.SearchQueryList
            .OfType<SearchQueryNear>()
            .FirstOrDefault(x => x.ChainedSearchParameter is null);

        if (searchQueryNear is null)
        {
            return null;
        }

        List<int> resourceStoreIdList = targetResourceStoreList
            .Where(x => x.ResourceStoreId.HasValue)
            .Select(x => x.ResourceStoreId!.Value)
            .ToList();

        return await nearDistanceQuery.GetNearestDistances(resourceStoreIdList, searchQueryNear);
    }
```

- [ ] **Step 5: Register in DI**

In `src/Abm.Pyro.Api/Program.cs`, beside `builder.Services.AddScoped<IResourceStoreSearch, ResourceStoreSearch>();`:

```csharp
    builder.Services.AddScoped<INearDistanceQuery, NearDistanceQuery>();
```

- [ ] **Step 6: Verify the build and full suite**

Run: `dotnet build src/Abm.Pyro.CI.slnf`
Expected: succeeds.

Run: `dotnet test src/Abm.Pyro.CI.slnf`
Expected: PASS. No behaviour is visible yet — the map is computed but nothing reads it. The extension arrives in Task 11.

- [ ] **Step 7: Commit**

```bash
git add src/Abm.Pyro.Domain src/Abm.Pyro.Repository src/Abm.Pyro.Api
git commit -m "feat: compute near distances for the matched page

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 11: Emit the location-distance extension

**Files:**
- Modify: `src/Abm.Pyro.Domain/FhirSupport/FhirBundleCreationCreationSupport.cs`
- Create: `src/Abm.Pyro.Api.Test/Search/NearDistanceExtensionTests.cs`

**Interfaces:**
- Consumes: `NearDistance` and `ResourceStoreSearchOutcome.NearDistanceByResourceStoreId` (Task 10), `NearDistanceUnitSupport` (Task 3).
- Produces: `FhirExtensionUrl.LocationDistance` constant in `Abm.Pyro.Domain.FhirSupport`.

- [ ] **Step 1: Write the failing extension tests**

`src/Abm.Pyro.Api.Test/Search/NearDistanceExtensionTests.cs`:

```csharp
using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Search;

public class NearDistanceExtensionTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string LocationDistanceUrl = "http://hl7.org/fhir/StructureDefinition/location-distance";

    private const decimal SydneyLatitude = -33.8568m;
    private const decimal SydneyLongitude = 151.2153m;

    // Roughly 1.1 km north-west of the Opera House
    private const decimal NearbyLatitude = -33.8500m;
    private const decimal NearbyLongitude = 151.2100m;

    [Fact]
    public async Task Search_Near_MatchedEntryCarriesTheDistanceExtension()
    {
        await CreateLocationAsync("Nearby", NearbyLatitude, NearbyLongitude);

        Bundle? bundle = await FhirClient.SearchAsync<Location>(
            new[] { "near=-33.8568|151.2153|5|km" });

        Assert.NotNull(bundle);
        Bundle.EntryComponent entry = Assert.Single(bundle.Entry);
        Assert.NotNull(entry.Search);

        Extension? extension = entry.Search.GetExtension(LocationDistanceUrl);
        Assert.NotNull(extension);

        var distance = Assert.IsType<Distance>(extension.Value);
        Assert.Equal("km", distance.Unit);
        Assert.Equal("km", distance.Code);
        Assert.Equal("http://unitsofmeasure.org", distance.System);
        Assert.NotNull(distance.Value);
        Assert.InRange(distance.Value.Value, 0.5m, 2.0m);
    }

    [Fact]
    public async Task Search_NearInMiles_ReportsTheDistanceInMiles()
    {
        await CreateLocationAsync("Nearby", NearbyLatitude, NearbyLongitude);

        Bundle? bundle = await FhirClient.SearchAsync<Location>(
            new[] { "near=-33.8568|151.2153|5|mi" });

        Assert.NotNull(bundle);
        Extension? extension = Assert.Single(bundle.Entry).Search.GetExtension(LocationDistanceUrl);
        Assert.NotNull(extension);

        var distance = Assert.IsType<Distance>(extension.Value);
        Assert.Equal("mi", distance.Unit);
        Assert.Equal("[mi_i]", distance.Code);
    }

    [Fact]
    public async Task Search_NearMultiplePositions_ReportsTheDistanceToTheClosest()
    {
        await CreateLocationAsync("Nearby", NearbyLatitude, NearbyLongitude);

        // The second position is the far side of the world; the closest must win.
        Bundle? bundle = await FhirClient.SearchAsync<Location>(
            new[] { "near=-33.8568|151.2153|5|km,0|0|5|km" });

        Assert.NotNull(bundle);
        Extension? extension = Assert.Single(bundle.Entry).Search.GetExtension(LocationDistanceUrl);
        Assert.NotNull(extension);

        var distance = Assert.IsType<Distance>(extension.Value);
        Assert.InRange(distance.Value!.Value, 0.5m, 2.0m);
    }

    [Fact]
    public async Task Search_WithoutANearTerm_HasNoDistanceExtension()
    {
        await CreateLocationAsync("Nearby", NearbyLatitude, NearbyLongitude);

        Bundle? bundle = await FhirClient.SearchAsync<Location>(new[] { "name=Nearby" });

        Assert.NotNull(bundle);
        Assert.Null(Assert.Single(bundle.Entry).Search.GetExtension(LocationDistanceUrl));
    }

    [Fact]
    public async Task Search_NearMissingTrue_HasNoDistanceExtension()
    {
        Location? noPosition = await FhirClient.CreateAsync(LocationBuilder.Build(name: "Nowhere"));
        Assert.NotNull(noPosition);

        Bundle? bundle = await FhirClient.SearchAsync<Location>(new[] { "near:missing=true" });

        Assert.NotNull(bundle);
        Assert.Null(Assert.Single(bundle.Entry).Search.GetExtension(LocationDistanceUrl));
    }

    /// <summary>
    /// With reporting turned off the same Locations must still match; only the extension goes.
    /// </summary>
    [Fact]
    public async Task Search_Near_WithDistanceReportingDisabled_StillMatchesButOmitsTheExtension()
    {
        await CreateLocationAsync("Nearby", NearbyLatitude, NearbyLongitude);

        using var factory = Fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LocationNear:ReturnDistanceInSearchResults"] = "false"
                })));

        using HttpClient httpClient = factory.CreateClient();
        var client = new FhirClient(
            new Uri(httpClient.BaseAddress!, "pyro/"),
            httpClient,
            new FhirClientSettings { PreferredFormat = ResourceFormat.Json });

        Bundle? bundle = await client.SearchAsync<Location>(new[] { "near=-33.8568|151.2153|5|km" });

        Assert.NotNull(bundle);
        Bundle.EntryComponent entry = Assert.Single(bundle.Entry);
        Assert.Null(entry.Search.GetExtension(LocationDistanceUrl));
    }

    private async Task CreateLocationAsync(string name, decimal latitude, decimal longitude)
    {
        Location? location = await FhirClient.CreateAsync(
            LocationBuilder.Build(name: name, latitude: latitude, longitude: longitude));
        Assert.NotNull(location);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter FullyQualifiedName~NearDistanceExtensionTests`
Expected: FAIL — the extension is absent, so the first three tests fail their `Assert.NotNull(extension)`.

- [ ] **Step 3: Add the extension URL constant**

In `src/Abm.Pyro.Domain/FhirSupport/`, create `FhirExtensionUrl.cs`:

```csharp
namespace Abm.Pyro.Domain.FhirSupport;

public static class FhirExtensionUrl
{
  /// <summary>
  /// Carries how far a matched Location is from the point given in a 'near' search. It lives on
  /// Bundle.entry.search rather than in the resource because the value depends on the search,
  /// not on the Location. See https://hl7.org/fhir/R4/location.html#positional
  /// </summary>
  public const string LocationDistance = "http://hl7.org/fhir/StructureDefinition/location-distance";
}
```

- [ ] **Step 4: Emit the extension during bundle creation**

In `src/Abm.Pyro.Domain/FhirSupport/FhirBundleCreationCreationSupport.cs`:

Add the usings:

```csharp
using Abm.Pyro.Domain.Support;
```

Change `CreateBundle` to pass the map down:

```csharp
    foreach (ResourceStore resourceStore in resourceStoreSearchOutcome.ResourceStoreList)
    {
      fhirBundle.Entry.Add(CreateEntry(resourceStore, bundleType, requestSchema, Bundle.SearchEntryMode.Match,
        resourceStoreSearchOutcome.NearDistanceByResourceStoreId));
    }
    foreach (ResourceStore resourceStore in resourceStoreSearchOutcome.IncludedResourceStoreList)
    {
      fhirBundle.Entry.Add(CreateEntry(resourceStore, bundleType, requestSchema, Bundle.SearchEntryMode.Include,
        nearDistanceByResourceStoreId: null));
    }
```

Included entries pass null deliberately: the extension describes a Location the client searched for, not one pulled in by `_include`.

Change the `CreateEntry` signature:

```csharp
  private Bundle.EntryComponent CreateEntry(ResourceStore resourceStore, Bundle.BundleType bundleType, string requestSchema,
    Bundle.SearchEntryMode searchEntryMode, IReadOnlyDictionary<int, NearDistance>? nearDistanceByResourceStoreId)
```

and inside the `if (bundleType == Bundle.BundleType.Searchset)` block, after `oResEntry.Link = new List<Bundle.LinkComponent>();`:

```csharp
      AddNearDistanceExtension(oResEntry, resourceStore, nearDistanceByResourceStoreId);
```

Add the method:

```csharp
  private static void AddNearDistanceExtension(Bundle.EntryComponent entry, ResourceStore resourceStore,
    IReadOnlyDictionary<int, NearDistance>? nearDistanceByResourceStoreId)
  {
    if (nearDistanceByResourceStoreId is null ||
        entry.Search is null ||
        !resourceStore.ResourceStoreId.HasValue ||
        !nearDistanceByResourceStoreId.TryGetValue(resourceStore.ResourceStoreId.Value, out NearDistance? nearDistance))
    {
      return;
    }

    double valueInReportUnit = NearDistanceUnitSupport.FromMetres(nearDistance.Metres, nearDistance.ReportUnit);

    entry.Search.AddExtension(FhirExtensionUrl.LocationDistance, new Distance
    {
      Value = Math.Round((decimal)valueInReportUnit, 3, MidpointRounding.AwayFromZero),
      Unit = NearDistanceUnitSupport.DisplayUnit(nearDistance.ReportUnit),
      System = "http://unitsofmeasure.org",
      Code = NearDistanceUnitSupport.UcumCode(nearDistance.ReportUnit)
    });
  }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter FullyQualifiedName~NearDistanceExtensionTests`
Expected: PASS, all six.

- [ ] **Step 6: Run the full suite**

Run: `dotnet test src/Abm.Pyro.CI.slnf`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Abm.Pyro.Domain src/Abm.Pyro.Api.Test
git commit -m "feat: return the location-distance extension on near search results

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 12: Chained and _has integration tests

Both chained and `_has` searches route their terminal parameter back through
`SearchSearchPredicateFactory.GetResourceStoreIndexPredicate`, which Task 8 taught to handle
`Special`. No production code should be needed. This task exists to prove that, and to fix it if
the claim turns out to be false.

**Files:**
- Create: `src/Abm.Pyro.Api.Test/Search/NearChainedSearchTests.cs`

**Interfaces:**
- Consumes: everything above.
- Produces: nothing.

- [ ] **Step 1: Write the tests**

`src/Abm.Pyro.Api.Test/Search/NearChainedSearchTests.cs`. Check `src/Abm.Pyro.Api.Test/Chaining/ChainedSearchTests.cs` and `ReverseChainSearchTests.cs` for the established way this codebase seeds linked resources, and follow it.

```csharp
using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Search;

public class NearChainedSearchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string LocationDistanceUrl = "http://hl7.org/fhir/StructureDefinition/location-distance";

    private const decimal SydneyLatitude = -33.8568m;
    private const decimal SydneyLongitude = 151.2153m;
    private const decimal MelbourneLatitude = -37.8183m;
    private const decimal MelbourneLongitude = 144.9671m;

    [Fact]
    public async Task Search_ChainedLocationNear_ReturnsTheReferencingResource()
    {
        Location sydney = await CreateLocationAsync("Sydney site", SydneyLatitude, SydneyLongitude);
        Location melbourne = await CreateLocationAsync("Melbourne site", MelbourneLatitude, MelbourneLongitude);

        await CreateEncounterAsync(sydney);
        await CreateEncounterAsync(melbourne);

        Bundle? bundle = await FhirClient.SearchAsync<Encounter>(
            new[] { "location.near=-33.8568|151.2153|5|km" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    /// <summary>
    /// A chained near filters correctly but reports no distance, because the matched resources
    /// are not Locations and the extension describes a Location entry.
    /// </summary>
    [Fact]
    public async Task Search_ChainedLocationNear_HasNoDistanceExtension()
    {
        Location sydney = await CreateLocationAsync("Sydney site", SydneyLatitude, SydneyLongitude);
        await CreateEncounterAsync(sydney);

        Bundle? bundle = await FhirClient.SearchAsync<Encounter>(
            new[] { "location.near=-33.8568|151.2153|5|km" });

        Assert.NotNull(bundle);
        Assert.Null(Assert.Single(bundle.Entry).Search.GetExtension(LocationDistanceUrl));
    }

    [Fact]
    public async Task Search_HasLocationNear_ReturnsTheMatchingLocation()
    {
        Location sydney = await CreateLocationAsync("Sydney site", SydneyLatitude, SydneyLongitude);
        Location melbourne = await CreateLocationAsync("Melbourne site", MelbourneLatitude, MelbourneLongitude);

        await CreateLocationPartOfAsync("Sydney ward", sydney);
        await CreateLocationPartOfAsync("Melbourne ward", melbourne);

        // Locations whose parent is near Sydney.
        Bundle? bundle = await FhirClient.SearchAsync<Location>(
            new[] { "partof.near=-33.8568|151.2153|5|km" });

        Assert.NotNull(bundle);
        Bundle.EntryComponent entry = Assert.Single(bundle.Entry);
        Assert.Equal("Sydney ward", ((Location)entry.Resource).Name);
    }

    private async Task<Location> CreateLocationAsync(string name, decimal latitude, decimal longitude)
    {
        Location? location = await FhirClient.CreateAsync(
            LocationBuilder.Build(name: name, latitude: latitude, longitude: longitude));
        Assert.NotNull(location);
        return location;
    }

    private async Task CreateLocationPartOfAsync(string name, Location parent)
    {
        Location child = LocationBuilder.Build(name: name);
        child.PartOf = new ResourceReference($"Location/{parent.Id}");

        Location? created = await FhirClient.CreateAsync(child);
        Assert.NotNull(created);
    }

    private async Task CreateEncounterAsync(Location location)
    {
        var encounter = new Encounter
        {
            Status = Encounter.EncounterStatus.Finished,
            Class = new Coding("http://terminology.hl7.org/CodeSystem/v3-ActCode", "AMB"),
            Location =
            {
                new Encounter.LocationComponent
                {
                    Location = new ResourceReference($"Location/{location.Id}")
                }
            }
        };

        Encounter? created = await FhirClient.CreateAsync(encounter);
        Assert.NotNull(created);
    }
}
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter FullyQualifiedName~NearChainedSearchTests`

Expected: PASS with no production change, confirming the claim that chaining comes free.

If they fail, the terminal-parameter routing is not reaching the new `Special` arm. Trace
`ChainedPredicateFactory.cs:85` and `HasPredicateFactory.cs:66` — both call
`searchPredicateFactory.GetResourceStoreIndexPredicate` — and fix whatever intercepts a
`SearchParamType.Special` parameter before it arrives there. Report the finding in the commit
message, since the spec asserts this path needs no work.

- [ ] **Step 3: Commit**

```bash
git add src/Abm.Pyro.Api.Test
git commit -m "test: chained and _has integration tests for Location near

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 13: CapabilityStatement check and documentation

**Files:**
- Create: `src/Abm.Pyro.Api.Test/Search/NearCapabilityStatementTests.cs`
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: everything above.
- Produces: nothing.

- [ ] **Step 1: Write the CapabilityStatement test**

`MetaDataService.cs:335` already maps `SearchParamType.Special` through to the FHIR enum, so this
should pass without a production change. It is a check, not a build.

`src/Abm.Pyro.Api.Test/Search/NearCapabilityStatementTests.cs`:

```csharp
using Abm.Pyro.Api.Test.Fixtures;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Search;

public class NearCapabilityStatementTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task CapabilityStatement_AdvertisesTheLocationNearSearchParameter()
    {
        CapabilityStatement? capabilityStatement = await FhirClient.CapabilityStatementAsync();

        Assert.NotNull(capabilityStatement);

        CapabilityStatement.ResourceComponent? location = capabilityStatement.Rest
            .SelectMany(x => x.Resource)
            .SingleOrDefault(x => x.Type == ResourceType.Location);

        Assert.NotNull(location);

        CapabilityStatement.SearchParamComponent? near =
            location.SearchParam.SingleOrDefault(x => x.Name == "near");

        Assert.NotNull(near);
        Assert.Equal(SearchParamType.Special, near.Type);
    }
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj --filter FullyQualifiedName~NearCapabilityStatementTests`
Expected: PASS.

If `near` is absent, find where the metadata service filters the search parameter list and stop it excluding `Special`. If `FhirClient.CapabilityStatementAsync()` is not the right call for this Firely version, fetch `/pyro/metadata` with the raw `HttpClient` and parse the body instead.

- [ ] **Step 3: Document the feature in CLAUDE.md**

Add a section after the "FHIRPath Patch" section:

```markdown
### Location `near` Search

Pyro supports the FHIR R4 `near` search parameter on `Location`
(`GET /{tenant}/Location?near=[latitude]|[longitude]|[distance]|[units]`), the only
search parameter of type `special` in R4. See
[Location §8.7.5.1](https://hl7.org/fhir/R4/location.html#positional).

**Value syntax.** `[latitude]|[longitude]|[distance]|[units]`, where distance and units are
optional. Note the order is latitude then longitude — the worked example in the spec has the
two transposed, which is a known erratum. Multiple positions are comma-separated and OR'd.

**Units.** Only `m`, `km` and miles (`[mi_i]`, `mi`, `mile`, `miles`) are supported. Omitted
units mean km. Anything else is a 400.

**Storage.** `Location.position` is indexed into the `IndexPosition` table as a SQL Server
`geography` point (SRID 4326) behind a `GEOGRAPHY_AUTO_GRID` spatial index created by raw SQL in
the `AddIndexPositionTable` migration. Filtering uses `STDistance(...) <= @radius`, which returns
metres — metres is the canonical unit everywhere below the parser.

**Coordinate order trap.** NetTopologySuite's `new Point(x, y)` is `new Point(longitude,
latitude)`, the opposite of T-SQL's `geography::Point(latitude, longitude, srid)`. A unit test in
`PositionSetterTest` asserts `Point.X == longitude`.

**Distance in results.** Each matched entry carries its distance as a `location-distance`
extension on `Bundle.entry.search`, computed by a page-scoped second query. Controlled by
`LocationNear:ReturnDistanceInSearchResults`; turning it off changes which fields come back, not
which Locations match. Chained and `_has` uses of `near` filter correctly but emit no extension,
because the matched resources are not Locations.

**Configuration** (`appsettings.json` → `LocationNear`): `DefaultDistanceInMetres` (the radius
used when the client omits the distance, default 10000), `MaximumDistanceInMetres` (default
1000000, above which a request is a 400), `ReturnDistanceInSearchResults` (default true).

**Known limitation.** Indexing runs only on create, update and patch, and the server has no
re-index facility, so Locations stored before this feature shipped have no `IndexPosition` row
and are invisible to `near` until they are next written.

**Implementation files:**

| File | Role |
|---|---|
| `Abm.Pyro.Domain/Model/IndexPosition.cs` | Index entity holding the geography point |
| `Abm.Pyro.Domain/IndexSetters/PositionSetter.cs` | `Location.position` → `IndexPosition` |
| `Abm.Pyro.Domain/SearchQueryEntity/SearchQueryNear.cs` | Parses the parameter value |
| `Abm.Pyro.Domain/Support/NearDistanceUnitSupport.cs` | The only place units and conversions live |
| `Abm.Pyro.Domain/Configuration/LocationNearSettings.cs` | Default radius, cap, reporting flag |
| `Abm.Pyro.Repository/Predicates/IndexPositionPredicateFactory.cs` | Builds `STDistance <= radius` |
| `Abm.Pyro.Repository/Query/NearDistanceQuery.cs` | Page-scoped distance computation (phase 2) |
```

Also update the `PyroDbContext` line in the "Repository / EF Core" section, which currently says
six `DbSet`s, to say seven and include `IndexPosition`. Update the Respawn list in the
"Integration Tests" section to include `IndexPosition`.

- [ ] **Step 4: Run the full suite one final time**

Run: `dotnet test src/Abm.Pyro.CI.slnf`
Expected: PASS, every test.

- [ ] **Step 5: Verify the migration snapshot is still clean**

```bash
cd src
dotnet ef migrations has-pending-model-changes --project Abm.Pyro.Repository --startup-project Abm.Pyro.Repository
```

Expected: "No changes have been made to the model since the last migration."

- [ ] **Step 6: Verify the snapshot is Linux-clean**

The CD pipeline runs `database update` on Linux and fails with `PendingModelChangesWarning` if the
snapshot differs by even a byte:

```bash
docker run --rm -v "$(pwd)":/src -w /src/src mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -c "dotnet tool install --global dotnet-ef && export PATH=\$PATH:/root/.dotnet/tools && \
           dotnet ef migrations has-pending-model-changes --project Abm.Pyro.Repository --startup-project Abm.Pyro.Repository"
```

Expected: the same "No changes" result as on Windows.

- [ ] **Step 7: Commit**

```bash
git add src/Abm.Pyro.Api.Test CLAUDE.md
git commit -m "docs: document the Location near search parameter

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

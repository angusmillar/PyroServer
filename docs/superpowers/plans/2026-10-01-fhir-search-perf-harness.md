# FHIR Search Performance Measurement Harness (Sub-Project A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `src/Abm.Pyro.Performance`, a developer-local tool that loads a Synthea FHIR R4 corpus through Pyro's own transaction pipeline, snapshots it, and measures every FHIR search access pattern into committed baselines carrying logical reads, plan operators, result cardinality and achieved selectivity.

**Architecture:** A .NET 10 console project hosts the **real** Pyro DI graph through `WebApplicationFactory<Program>` — the only way to obtain the genuine container without duplicating `Program.cs`'s registration. Loading dispatches `FhirBatchOrTransactionRequest` through the real `IRequestDispatcher` in-process, so the real index setters run and no code in the harness reimplements indexing. A `SnapshotManager` takes a SQL `.bak` after load, making reset cheap and baselines comparable. A `CorpusProfiler` derives selectivity statistics from the loaded index tables so query values are declared by selectivity rather than hard-coded. A `QueryRunner` drives `ISearchQueryService` → `IResourceStoreSearch` and captures emitted SQL with an EF command interceptor; `PlanAnalyser` re-executes it under `SET STATISTICS IO, XML ON`. Nothing in the production projects changes.

**Tech Stack:** .NET 10, `Microsoft.AspNetCore.Mvc.Testing` 10.0.12 (DI host), `Testcontainers.MsSql` 4.15.0, `Microsoft.Data.SqlClient` 7.0.3, `Hl7.Fhir.R4` 6.5.0, xUnit 2.9.3. CLI arguments are parsed by a hand-rolled parser in Task 1 — no `System.CommandLine` dependency, so the project has no prerelease packages.

**Spec:** `docs/superpowers/specs/2026-10-01-fhir-search-perf-harness-design.md` — Part II (§6–§20). Part I is programme context; this plan implements Part II only.

## Correction to the spec, found while researching this plan

Spec §9 precondition 2 says "FHIR profile validation is off for the performance tenant", implying the harness must configure it. **It is already off by default.** `FhirValidationSettings.SeedDefaultSettings()` (`src/Abm.Pyro.Domain/ServiceSettings/FhirValidationSettings.cs`) seeds the `ServiceSetting` row with `"ValidateOnCreate":false,"ValidateOnUpdate":false`, and that row is created by EF migration. So the precondition becomes an **assertion** (Task 4), not a configuration step. The spec's caveat still stands and still belongs in every write-cost figure: measurements exclude profile-validation cost — which is also true of a default Pyro install.

## Global Constraints

- **Target framework `net10.0`, `LangVersion 14`, `Nullable enable`, `ImplicitUsings enable`** — matching every other .NET 10 project in `src/`.
- **New projects MUST be added to `src/Abm.Pyro.CI.slnf`** (`CLAUDE.md`) or CI will not compile them, **and** to `src/Abm.Pyro.sln`.
- **Sub-project A changes no production behaviour** (spec §6). No file under `Abm.Pyro.Api`, `Abm.Pyro.Application`, `Abm.Pyro.Domain` or `Abm.Pyro.Repository` is modified by any task in this plan.
- **No EF migration is added.** Index changes belong to sub-project D.
- **The `PerfCorpusManifest` table is created by raw SQL from the harness**, never by an EF migration, and EF has no entity for it (spec §10.4).
- **The corpus path has no default pointing inside the repository tree** (spec §5.3, §8). Absent configuration is an error, not a fallback.
- **`--max-files` defaults to every `*.json` in the directory.** The numbers 1,180 and 527,113 are measured facts about one export (spec §5.1) and MUST NOT appear as constants in code.
- **`using Task = System.Threading.Tasks.Task;`** in any file that also uses `Hl7.Fhir.Model` (`CLAUDE.md` coding conventions).
- **Database and backup live in named Docker volumes**, never host bind mounts (spec §5.3, §10.2).
- **Docker must be running** for every task from Task 3 onward.
- **No metric is ever recorded as null, zero or defaulted on a parse or execution failure** (spec §16). Fail loudly.

## Review Focus

Five input classes the spec implies but no task's own happy path exercises. Each has its test added to the owning task.

1. **A file that is valid JSON but not a `type: transaction` Bundle** — a `collection` Bundle, or a bare `Patient`. Must name the file and stop the load, not skip it silently and produce a census that understates the corpus. *(Task 2)*
2. **A transaction entry that fails mid-bundle** — the whole bundle rolls back, so the database is short by that bundle's resources while the loader's running count already included them. The manifest would then claim a census the database does not hold. Must abort the load naming the file, not continue to the next one. *(Task 5)*
3. **A query-set role no value in the corpus satisfies** — asking for "≈20% selective" when the hottest value is 0.5%. Must fail loudly rather than silently resolving to the nearest available value and recording a measurement nobody can interpret. *(Task 11)*
4. **`reset` when a migration has been added since the snapshot** (spec §20.2) — the `.bak` carries the old schema and `__EFMigrationsHistory`. Restoring it and measuring would silently measure the wrong schema, which is exactly what sub-project D would do. Must detect pending migrations after restore and apply them, recording that it did. *(Task 6)*
5. **A captured SQL statement that cannot be re-executed, or plan XML that will not parse** — must fail loudly and never record a null, zero or defaulted metric. *(Tasks 7 and 9)*

---

## File Structure

```
src/Abm.Pyro.Performance/
  Abm.Pyro.Performance.csproj
  appsettings.json                        corpus path, container, perf tenant (no secrets)
  Program.cs                              CLI root; subcommand dispatch only
  Cli/
    ArgumentParser.cs                     hand-rolled --flag value parser
    CommandResult.cs                      exit code + message
    LoadCommand.cs
    SnapshotCommand.cs
    ResetCommand.cs
    ProfileCommand.cs
    RunCommand.cs
    CompareCommand.cs
    IngestCommand.cs
    ProbeCommand.cs
  Configuration/
    PerformanceSettings.cs                bound options
  Corpus/
    CorpusFile.cs                         record (FileName, ByteSize)
    CorpusDirectoryScanner.cs             path + maxFiles -> ordered CorpusFile[]
    CorpusIdentity.cs                     hash over loader version + files + maxFiles
    ResourceCensus.cs                     per-type counts
    CorpusManifest.cs                     identity + census + counts + timings
  Loading/
    BundleReader.cs                       file -> Bundle, validated
    LoadPreconditions.cs                  empty-db, endpoint policy, validation-off
    LoadProgress.cs                       throughput accounting
    CorpusLoader.cs                       scan -> preconditions -> dispatch -> manifest
    ManifestStore.cs                      PerfCorpusManifest table read/write
    TableSizeReporter.cs                  sp_spaceused per table
  Hosting/
    PerformanceHost.cs                    container + factory lifecycle
    PerformanceWebApplicationFactory.cs   DI host
    TenantScope.cs                        DI scope with the tenant set
  Snapshots/
    SnapshotManager.cs                    BACKUP / RESTORE + connection drain
  Profiling/
    SelectivityRole.cs                    role declaration (Hot/Mid/Cold/Percentile/Sweep)
    ProfileEntry.cs                       one parameter's value statistics
    CorpusProfile.cs                      the whole profile
    CorpusProfiler.cs                     index tables -> CorpusProfile
    ProfileStore.cs                       JSON read/write
    ValueRoleResolver.cs                  role -> literal + achieved selectivity
  Measurement/
    CapturedCommand.cs                    one captured SQL statement
    SqlCaptureInterceptor.cs              DbCommandInterceptor
    QueryRunner.cs                        query string -> pipeline -> CapturedCommand[]
    StatisticsIoParser.cs                 InfoMessage text -> per-table logical reads
    PlanXmlParser.cs                      plan XML -> operator list
    PlanAnalyser.cs                       captured SQL -> QueryMetrics
    IndexShapeProbe.cs                    hand-written SQL microbenchmarks
  Baselines/
    QueryDefinition.cs                    one query-set entry
    QuerySetLoader.cs                     assets/perf/queries/*.json
    BaselineEntry.cs                      one measured query
    Baseline.cs                           a whole run
    BaselineStore.cs                      JSON read/write + identity guard
    ReportWriter.cs                       markdown comparison

src/Abm.Pyro.Performance.Test/
  Abm.Pyro.Performance.Test.csproj
  Corpus/CorpusDirectoryScannerTest.cs
  Corpus/CorpusIdentityTest.cs
  Corpus/CorpusManifestTest.cs
  Loading/BundleReaderTest.cs
  Profiling/ValueRoleResolverTest.cs
  Measurement/StatisticsIoParserTest.cs
  Measurement/PlanXmlParserTest.cs
  Baselines/QuerySetLoaderTest.cs
  Baselines/BaselineStoreTest.cs
  Baselines/ReportWriterTest.cs
  Integration/PerformanceHostFixture.cs       shared container + host
  Integration/LoadPreconditionsTest.cs
  Integration/CorpusLoaderTest.cs
  Integration/SnapshotManagerTest.cs
  Integration/CorpusProfilerTest.cs
  Integration/QueryRunnerTest.cs
  Integration/PlanAnalyserTest.cs
  Integration/RunCommandTest.cs
  Integration/IngestCommandTest.cs
  Integration/IndexShapeProbeTest.cs
  Support/SyntheticBundleBuilder.cs           builds test transaction bundles in code
  Assets/sample-plan-seek.xml
  Assets/sample-plan-scan.xml
  Assets/sample-statistics-io.txt

assets/perf/
  queries/*.json                          the §14 query set
  corpus-manifest.json                    written by load
  corpus-profile.json                     written by profile
  baselines/*.json                        committed baseline runs
```

**No Synthea data is committed.** Integration tests build their own transaction bundles in code via `SyntheticBundleBuilder` and write them to a temp directory. The real corpus is only ever read from the configured path.

---

### Task 1: Project scaffold, corpus scanning, identity and manifest

The corpus's identity is what every later task checks against (spec §8, §16), so it comes first and needs no database.

**Files:**
- Create: `src/Abm.Pyro.Performance/Abm.Pyro.Performance.csproj`
- Create: `src/Abm.Pyro.Performance/appsettings.json`
- Create: `src/Abm.Pyro.Performance/Program.cs`
- Create: `src/Abm.Pyro.Performance/Cli/ArgumentParser.cs`
- Create: `src/Abm.Pyro.Performance/Cli/CommandResult.cs`
- Create: `src/Abm.Pyro.Performance/Configuration/PerformanceSettings.cs`
- Create: `src/Abm.Pyro.Performance/Corpus/CorpusFile.cs`
- Create: `src/Abm.Pyro.Performance/Corpus/CorpusDirectoryScanner.cs`
- Create: `src/Abm.Pyro.Performance/Corpus/CorpusIdentity.cs`
- Create: `src/Abm.Pyro.Performance/Corpus/ResourceCensus.cs`
- Create: `src/Abm.Pyro.Performance/Corpus/CorpusManifest.cs`
- Create: `src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj`
- Test: `src/Abm.Pyro.Performance.Test/Corpus/CorpusDirectoryScannerTest.cs`
- Test: `src/Abm.Pyro.Performance.Test/Corpus/CorpusIdentityTest.cs`
- Test: `src/Abm.Pyro.Performance.Test/Corpus/CorpusManifestTest.cs`
- Modify: `src/Abm.Pyro.sln`
- Modify: `src/Abm.Pyro.CI.slnf`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `record CorpusFile(string FileName, long ByteSize)`
  - `static class CorpusDirectoryScanner` → `IReadOnlyList<CorpusFile> Scan(string directoryPath, int? maxFiles)`
  - `static class CorpusIdentity` → `string Compute(string loaderVersion, IReadOnlyList<CorpusFile> files, int? maxFiles)` (lower-case hex SHA-256)
  - `const string CorpusIdentity.LoaderVersion = "1"`
  - `record ResourceCensus(IReadOnlyDictionary<string, int> CountByResourceType)` with `int Total`
  - `record CorpusManifest(string CorpusIdentityHash, string LoaderVersion, string CorpusDirectory, int? MaxFiles, IReadOnlyList<CorpusFile> Files, ResourceCensus Census, int ResourceStoreRowCount, DateTimeOffset LoadedAtUtc, double LoadSeconds)` with `static string ToJson(CorpusManifest)` and `static CorpusManifest FromJson(string)`
  - `record CommandResult(int ExitCode, string Message)` with `static CommandResult Ok(string)` and `static CommandResult Fail(string)`
  - `class ArgumentParser` → `string? GetString(string flag)`, `int? GetInt(string flag)`, `bool HasFlag(string flag)`
  - `class PerformanceSettings` with `string? CorpusDirectory`, `string ContainerMemoryLimitBytes`, `string DatabaseName`, `string BackupVolumeName`, `string TenantCode`, `string TenantUrlCode`

- [ ] **Step 1: Create the two project files and register them in the solution and CI filter**

`src/Abm.Pyro.Performance/Abm.Pyro.Performance.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <LangVersion>14</LangVersion>
        <IsPackable>false</IsPackable>
        <RootNamespace>Abm.Pyro.Performance</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
        <PackageReference Include="Testcontainers.MsSql" Version="4.15.0" />
        <PackageReference Include="Microsoft.Data.SqlClient" Version="7.0.3" />
        <PackageReference Include="Hl7.Fhir.R4" Version="6.5.0" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\Abm.Pyro.Api\Abm.Pyro.Api.csproj" />
    </ItemGroup>

    <ItemGroup>
        <None Update="appsettings.json">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </None>
    </ItemGroup>

</Project>
```

`src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <LangVersion>14</LangVersion>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.10.1" />
        <PackageReference Include="xunit" Version="2.9.3" />
        <PackageReference Include="xunit.runner.visualstudio" Version="4.0.0">
            <PrivateAssets>all</PrivateAssets>
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        </PackageReference>
        <PackageReference Include="coverlet.collector" Version="10.0.1">
            <PrivateAssets>all</PrivateAssets>
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        </PackageReference>
    </ItemGroup>

    <ItemGroup>
        <Using Include="Xunit" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\Abm.Pyro.Performance\Abm.Pyro.Performance.csproj" />
    </ItemGroup>

    <ItemGroup>
        <None Update="Assets\**\*">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </None>
    </ItemGroup>

</Project>
```

Then register both:

```bash
cd src
dotnet sln Abm.Pyro.sln add Abm.Pyro.Performance/Abm.Pyro.Performance.csproj
dotnet sln Abm.Pyro.sln add Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj
```

Add both project paths to the `projects` array in `src/Abm.Pyro.CI.slnf`, using the same backslash path style as the existing entries.

- [ ] **Step 2: Write the failing scanner tests**

`src/Abm.Pyro.Performance.Test/Corpus/CorpusDirectoryScannerTest.cs`:

```csharp
using Abm.Pyro.Performance.Corpus;

namespace Abm.Pyro.Performance.Test.Corpus;

public class CorpusDirectoryScannerTest : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pyro-perf-scan").FullName;

    private void WriteFile(string name, int bytes) =>
        File.WriteAllText(Path.Combine(_dir, name), new string('x', bytes));

    [Fact]
    public void Scan_ReturnsFilesInSortedNameOrder()
    {
        WriteFile("c.json", 3);
        WriteFile("a.json", 1);
        WriteFile("b.json", 2);

        var files = CorpusDirectoryScanner.Scan(_dir, maxFiles: null);

        Assert.Equal(["a.json", "b.json", "c.json"], files.Select(f => f.FileName));
    }

    [Fact]
    public void Scan_RecordsByteSize()
    {
        WriteFile("a.json", 42);

        var files = CorpusDirectoryScanner.Scan(_dir, maxFiles: null);

        Assert.Equal(42, files.Single().ByteSize);
    }

    [Fact]
    public void Scan_MaxFilesTakesThePrefixOfSortedOrder()
    {
        WriteFile("a.json", 1);
        WriteFile("b.json", 1);
        WriteFile("c.json", 1);

        var files = CorpusDirectoryScanner.Scan(_dir, maxFiles: 2);

        Assert.Equal(["a.json", "b.json"], files.Select(f => f.FileName));
    }

    [Fact]
    public void Scan_IgnoresNonJsonFiles()
    {
        WriteFile("a.json", 1);
        WriteFile("readme.txt", 1);

        var files = CorpusDirectoryScanner.Scan(_dir, maxFiles: null);

        Assert.Equal("a.json", files.Single().FileName);
    }

    [Fact]
    public void Scan_MissingDirectoryThrowsNamingThePath()
    {
        string missing = Path.Combine(_dir, "nope");

        var ex = Assert.Throws<DirectoryNotFoundException>(() => CorpusDirectoryScanner.Scan(missing, null));

        Assert.Contains(missing, ex.Message);
    }

    [Fact]
    public void Scan_EmptyDirectoryThrows()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CorpusDirectoryScanner.Scan(_dir, null));

        Assert.Contains("no *.json", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Scan_NonPositiveMaxFilesThrows(int maxFiles)
    {
        WriteFile("a.json", 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => CorpusDirectoryScanner.Scan(_dir, maxFiles));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
```

- [ ] **Step 3: Run the scanner tests and verify they fail**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~CorpusDirectoryScannerTest"`
Expected: FAIL — `CorpusDirectoryScanner` does not exist (compile error).

- [ ] **Step 4: Implement `CorpusFile` and `CorpusDirectoryScanner`**

`src/Abm.Pyro.Performance/Corpus/CorpusFile.cs`:

```csharp
namespace Abm.Pyro.Performance.Corpus;

/// <summary>
/// One corpus input file, identified by name and size. Name plus size is what the corpus
/// identity hash is built from — enough to catch a different export, a half-copied
/// directory or a changed subset, without reading 1.3 GB of JSON.
/// </summary>
public record CorpusFile(string FileName, long ByteSize);
```

`src/Abm.Pyro.Performance/Corpus/CorpusDirectoryScanner.cs`:

```csharp
namespace Abm.Pyro.Performance.Corpus;

/// <summary>
/// Enumerates the corpus directory in sorted filename order — the only ordering the harness
/// imposes, and what makes a --max-files subset well defined and reproducible.
/// </summary>
public static class CorpusDirectoryScanner
{
    public static IReadOnlyList<CorpusFile> Scan(string directoryPath, int? maxFiles)
    {
        if (maxFiles is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxFiles), maxFiles, "--max-files must be a positive number of files.");
        }

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Corpus directory not found: '{directoryPath}'.");
        }

        List<CorpusFile> files = Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => new CorpusFile(Path.GetFileName(path), new FileInfo(path).Length))
            .OrderBy(file => file.FileName, StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
        {
            throw new InvalidOperationException(
                $"Corpus directory '{directoryPath}' contains no *.json files.");
        }

        return maxFiles is null ? files : files.Take(maxFiles.Value).ToList();
    }
}
```

- [ ] **Step 5: Run the scanner tests and verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~CorpusDirectoryScannerTest"`
Expected: PASS — 8 tests.

- [ ] **Step 6: Write the failing identity tests**

`src/Abm.Pyro.Performance.Test/Corpus/CorpusIdentityTest.cs`:

```csharp
using Abm.Pyro.Performance.Corpus;

namespace Abm.Pyro.Performance.Test.Corpus;

public class CorpusIdentityTest
{
    private static readonly IReadOnlyList<CorpusFile> Files =
    [
        new CorpusFile("a.json", 10),
        new CorpusFile("b.json", 20),
    ];

    [Fact]
    public void Compute_IsStableForTheSameInput()
    {
        string first = CorpusIdentity.Compute("1", Files, maxFiles: null);
        string second = CorpusIdentity.Compute("1", Files, maxFiles: null);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Compute_ChangesWhenAFileSizeChanges()
    {
        IReadOnlyList<CorpusFile> changed = [new CorpusFile("a.json", 11), new CorpusFile("b.json", 20)];

        Assert.NotEqual(
            CorpusIdentity.Compute("1", Files, null),
            CorpusIdentity.Compute("1", changed, null));
    }

    [Fact]
    public void Compute_ChangesWhenAFileIsAdded()
    {
        IReadOnlyList<CorpusFile> added = [.. Files, new CorpusFile("c.json", 30)];

        Assert.NotEqual(
            CorpusIdentity.Compute("1", Files, null),
            CorpusIdentity.Compute("1", added, null));
    }

    [Fact]
    public void Compute_ChangesWhenTheLoaderVersionChanges()
    {
        Assert.NotEqual(
            CorpusIdentity.Compute("1", Files, null),
            CorpusIdentity.Compute("2", Files, null));
    }

    [Fact]
    public void Compute_ChangesWhenMaxFilesChanges()
    {
        Assert.NotEqual(
            CorpusIdentity.Compute("1", Files, null),
            CorpusIdentity.Compute("1", Files, maxFiles: 2));
    }

    [Fact]
    public void Compute_IsIndependentOfTheInputCollectionOrder()
    {
        IReadOnlyList<CorpusFile> reversed = [new CorpusFile("b.json", 20), new CorpusFile("a.json", 10)];

        Assert.Equal(
            CorpusIdentity.Compute("1", Files, null),
            CorpusIdentity.Compute("1", reversed, null));
    }

    [Fact]
    public void Compute_ReturnsLowerCaseHexSha256()
    {
        string hash = CorpusIdentity.Compute("1", Files, null);

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }
}
```

- [ ] **Step 7: Run the identity tests and verify they fail**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~CorpusIdentityTest"`
Expected: FAIL — `CorpusIdentity` does not exist.

- [ ] **Step 8: Implement `CorpusIdentity`**

`src/Abm.Pyro.Performance/Corpus/CorpusIdentity.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Abm.Pyro.Performance.Corpus;

/// <summary>
/// The corpus fingerprint (spec §8). Hashes the loader version, --max-files and the ordered
/// (filename, size) list. Sorting here rather than trusting the caller means the hash is a
/// property of the file set, not of how it happened to be enumerated.
/// </summary>
public static class CorpusIdentity
{
    /// <summary>
    /// Bump this whenever a loader change would alter what ends up in the database for the
    /// same input files, so old manifests stop matching and refuse to be measured against.
    /// </summary>
    public const string LoaderVersion = "1";

    public static string Compute(string loaderVersion, IReadOnlyList<CorpusFile> files, int? maxFiles)
    {
        var builder = new StringBuilder();
        builder.Append("loader=").Append(loaderVersion).Append('\n');
        builder.Append("maxFiles=").Append(maxFiles?.ToString() ?? "all").Append('\n');

        foreach (CorpusFile file in files.OrderBy(f => f.FileName, StringComparer.Ordinal))
        {
            builder.Append(file.FileName).Append('=').Append(file.ByteSize).Append('\n');
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexStringLower(hash);
    }
}
```

- [ ] **Step 9: Run the identity tests and verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~CorpusIdentityTest"`
Expected: PASS — 7 tests.

- [ ] **Step 10: Write the failing manifest round-trip test**

`src/Abm.Pyro.Performance.Test/Corpus/CorpusManifestTest.cs`:

```csharp
using Abm.Pyro.Performance.Corpus;

namespace Abm.Pyro.Performance.Test.Corpus;

public class CorpusManifestTest
{
    private static CorpusManifest Sample() => new(
        CorpusIdentityHash: new string('a', 64),
        LoaderVersion: "1",
        CorpusDirectory: @"C:\Temp\synthea",
        MaxFiles: null,
        Files: [new CorpusFile("a.json", 10)],
        Census: new ResourceCensus(new Dictionary<string, int> { ["Patient"] = 2, ["Observation"] = 7 }),
        ResourceStoreRowCount: 9,
        LoadedAtUtc: new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero),
        LoadSeconds: 123.5);

    [Fact]
    public void ToJson_FromJson_RoundTrips()
    {
        CorpusManifest original = Sample();

        CorpusManifest result = CorpusManifest.FromJson(CorpusManifest.ToJson(original));

        Assert.Equal(original.CorpusIdentityHash, result.CorpusIdentityHash);
        Assert.Equal(original.CorpusDirectory, result.CorpusDirectory);
        Assert.Equal(original.ResourceStoreRowCount, result.ResourceStoreRowCount);
        Assert.Equal(original.LoadSeconds, result.LoadSeconds);
        Assert.Equal("a.json", result.Files.Single().FileName);
        Assert.Equal(7, result.Census.CountByResourceType["Observation"]);
    }

    [Fact]
    public void Census_TotalSumsEveryResourceType()
    {
        Assert.Equal(9, Sample().Census.Total);
    }

    [Fact]
    public void Census_TotalOfAnEmptyCensusIsZero()
    {
        Assert.Equal(0, new ResourceCensus(new Dictionary<string, int>()).Total);
    }
}
```

- [ ] **Step 11: Run it and verify it fails**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~CorpusManifestTest"`
Expected: FAIL — `CorpusManifest` does not exist.

- [ ] **Step 12: Implement `ResourceCensus` and `CorpusManifest`**

`src/Abm.Pyro.Performance/Corpus/ResourceCensus.cs`:

```csharp
namespace Abm.Pyro.Performance.Corpus;

/// <summary>
/// Per-resource-type counts. Resource count is an output of loading, not an input to it
/// (spec §8), so this is produced by scanning or loading and then recorded.
/// </summary>
public record ResourceCensus(IReadOnlyDictionary<string, int> CountByResourceType)
{
    public int Total => CountByResourceType.Values.Sum();
}
```

`src/Abm.Pyro.Performance/Corpus/CorpusManifest.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Abm.Pyro.Performance.Corpus;

/// <summary>
/// What was loaded, and from what. Written both to assets/perf/corpus-manifest.json and to
/// the PerfCorpusManifest table inside the database, so a restored .bak self-identifies
/// (spec §10.4).
/// </summary>
public record CorpusManifest(
    string CorpusIdentityHash,
    string LoaderVersion,
    string CorpusDirectory,
    int? MaxFiles,
    IReadOnlyList<CorpusFile> Files,
    ResourceCensus Census,
    int ResourceStoreRowCount,
    DateTimeOffset LoadedAtUtc,
    double LoadSeconds)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string ToJson(CorpusManifest manifest) => JsonSerializer.Serialize(manifest, Options);

    public static CorpusManifest FromJson(string json) =>
        JsonSerializer.Deserialize<CorpusManifest>(json, Options)
        ?? throw new InvalidOperationException("Corpus manifest JSON deserialised to null.");
}
```

- [ ] **Step 13: Run it and verify it passes**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~CorpusManifestTest"`
Expected: PASS — 3 tests.

- [ ] **Step 14: Add settings, the argument parser and a Program.cs that dispatches nothing yet**

`src/Abm.Pyro.Performance/appsettings.json` — note `CorpusDirectory` is deliberately `null`, so an unconfigured run fails rather than falling back to a repo path (spec §5.3):

```json
{
  "Performance": {
    "CorpusDirectory": null,
    "ContainerMemoryLimitBytes": 4294967296,
    "DatabaseName": "PyroPerf",
    "BackupVolumeName": "pyro-perf-backup",
    "TenantCode": "Pyro",
    "TenantUrlCode": "pyro"
  }
}
```

`src/Abm.Pyro.Performance/Configuration/PerformanceSettings.cs`:

```csharp
namespace Abm.Pyro.Performance.Configuration;

public class PerformanceSettings
{
    public const string SectionName = "Performance";

    /// <summary>
    /// Absolute path to the Synthea bundle directory. Deliberately nullable with no default:
    /// a default pointing inside the repository tree is how 1.3 GB gets committed by accident
    /// (spec §5.3).
    /// </summary>
    public string? CorpusDirectory { get; set; }

    public long ContainerMemoryLimitBytes { get; set; } = 4L * 1024 * 1024 * 1024;
    public string DatabaseName { get; set; } = "PyroPerf";
    public string BackupVolumeName { get; set; } = "pyro-perf-backup";
    public string TenantCode { get; set; } = "Pyro";
    public string TenantUrlCode { get; set; } = "pyro";

    public string RequireCorpusDirectory() =>
        string.IsNullOrWhiteSpace(CorpusDirectory)
            ? throw new InvalidOperationException(
                "Performance:CorpusDirectory is not configured. Set it in appsettings.json, " +
                "via the PYRO_PERF_CORPUS environment variable, or with --corpus. " +
                "There is deliberately no default.")
            : CorpusDirectory;
}
```

`src/Abm.Pyro.Performance/Cli/CommandResult.cs`:

```csharp
namespace Abm.Pyro.Performance.Cli;

public record CommandResult(int ExitCode, string Message)
{
    public static CommandResult Ok(string message) => new(0, message);
    public static CommandResult Fail(string message) => new(1, message);
}
```

`src/Abm.Pyro.Performance/Cli/ArgumentParser.cs`:

```csharp
namespace Abm.Pyro.Performance.Cli;

/// <summary>
/// Minimal "--flag value" / "--flag" parser. Hand-rolled on purpose: the harness is a
/// developer tool and this keeps the project free of prerelease dependencies.
/// </summary>
public class ArgumentParser
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);

    public ArgumentParser(IReadOnlyList<string> args)
    {
        for (int i = 0; i < args.Count; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            string key = args[i][2..];
            bool hasValue = i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
            _values[key] = hasValue ? args[++i] : null;
        }
    }

    public bool HasFlag(string flag) => _values.ContainsKey(flag);

    public string? GetString(string flag) => _values.TryGetValue(flag, out string? value) ? value : null;

    public int? GetInt(string flag)
    {
        string? raw = GetString(flag);

        if (raw is null)
        {
            return null;
        }

        return int.TryParse(raw, out int parsed)
            ? parsed
            : throw new ArgumentException($"--{flag} expects an integer but got '{raw}'.");
    }
}
```

`src/Abm.Pyro.Performance/Program.cs`:

```csharp
using Abm.Pyro.Performance.Cli;

if (args.Length == 0)
{
    Console.WriteLine(
        """
        Pyro FHIR search performance harness.

          load      --corpus <path> [--max-files N] [--force]
          snapshot
          reset
          profile
          run       --queries <set> --baseline <name>
          compare   --from <name> --to <name>
          ingest    --count N
          probe
        """);
    return 1;
}

var parser = new ArgumentParser(args[1..]);

CommandResult result = args[0].ToLowerInvariant() switch
{
    _ => CommandResult.Fail($"Unknown command '{args[0]}'."),
};

Console.WriteLine(result.Message);
return result.ExitCode;
```

Each later task adds its own arm to that `switch`.

- [ ] **Step 15: Verify the whole solution filter still builds**

Run: `dotnet build src/Abm.Pyro.CI.slnf`
Expected: Build succeeded, including the two new projects.

- [ ] **Step 16: Run every test in the new project**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj`
Expected: PASS — 18 tests.

- [ ] **Step 17: Commit**

```bash
git add src/Abm.Pyro.Performance src/Abm.Pyro.Performance.Test src/Abm.Pyro.sln src/Abm.Pyro.CI.slnf
git commit -m "feat(perf): scaffold the performance harness with corpus scanning and identity

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 2: Bundle reader and directory census

The loader must know what is in the directory before it starts, both to census it and to check the endpoint policy against the types present (spec §9). A file that is not a transaction Bundle must stop the load naming the file — Review Focus 1.

**Files:**
- Create: `src/Abm.Pyro.Performance/Loading/BundleReader.cs`
- Create: `src/Abm.Pyro.Performance.Test/Support/SyntheticBundleBuilder.cs`
- Test: `src/Abm.Pyro.Performance.Test/Loading/BundleReaderTest.cs`

**Interfaces:**
- Consumes: `CorpusFile`, `CorpusDirectoryScanner`, `ResourceCensus` (Task 1).
- Produces:
  - `class BundleReader` with `Bundle Read(string directoryPath, CorpusFile file)` and `ResourceCensus Census(string directoryPath, IReadOnlyList<CorpusFile> files)`
  - `class CorpusFileFormatException : Exception`
  - `static class SyntheticBundleBuilder` (test support) with `static Bundle TransactionBundle(params (string ResourceType, string FullUuid)[] entries)`, `static string WriteTo(string directory, string fileName, Resource resource)`

- [ ] **Step 1: Write the test support builder**

`src/Abm.Pyro.Performance.Test/Support/SyntheticBundleBuilder.cs`:

```csharp
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Test.Support;

/// <summary>
/// Builds Synthea-shaped transaction bundles in code. No Synthea data is committed to the
/// repository, so every test that needs a corpus builds its own.
/// </summary>
public static class SyntheticBundleBuilder
{
    /// <summary>
    /// A transaction bundle shaped like Synthea's: POST entries, urn:uuid fullUrls, and
    /// Observations referencing the Patient by its urn:uuid.
    /// </summary>
    public static Bundle PatientWithObservations(string patientUuid, int observationCount, string observationCode)
    {
        var bundle = new Bundle { Type = Bundle.BundleType.Transaction };

        bundle.Entry.Add(new Bundle.EntryComponent
        {
            FullUrl = $"urn:uuid:{patientUuid}",
            Resource = new Patient
            {
                Name = [new HumanName { Family = "Testerson", Given = ["Ada"] }],
                Gender = AdministrativeGender.Female,
                BirthDate = "1980-01-01",
            },
            Request = new Bundle.RequestComponent { Method = Bundle.HTTPVerb.POST, Url = "Patient" },
        });

        for (int i = 0; i < observationCount; i++)
        {
            bundle.Entry.Add(new Bundle.EntryComponent
            {
                FullUrl = $"urn:uuid:{Guid.Parse($"00000000-0000-0000-0000-{i:D12}")}",
                Resource = new Observation
                {
                    Status = ObservationStatus.Final,
                    Code = new CodeableConcept("http://loinc.org", observationCode),
                    Subject = new ResourceReference($"urn:uuid:{patientUuid}"),
                    Effective = new FhirDateTime("2024-06-01T00:00:00Z"),
                    Value = new Quantity(72 + i, "mm[Hg]", "http://unitsofmeasure.org"),
                },
                Request = new Bundle.RequestComponent { Method = Bundle.HTTPVerb.POST, Url = "Observation" },
            });
        }

        return bundle;
    }

    public static string WriteTo(string directory, string fileName, Resource resource)
    {
        string path = Path.Combine(directory, fileName);
        File.WriteAllText(path, new FhirJsonSerializer().SerializeToString(resource));
        return path;
    }

    public static string WriteRaw(string directory, string fileName, string content)
    {
        string path = Path.Combine(directory, fileName);
        File.WriteAllText(path, content);
        return path;
    }
}
```

- [ ] **Step 2: Write the failing reader tests**

`src/Abm.Pyro.Performance.Test/Loading/BundleReaderTest.cs`:

```csharp
using Abm.Pyro.Performance.Corpus;
using Abm.Pyro.Performance.Loading;
using Abm.Pyro.Performance.Test.Support;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Performance.Test.Loading;

public class BundleReaderTest : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pyro-perf-read").FullName;
    private readonly BundleReader _reader = new();

    [Fact]
    public void Read_ReturnsATransactionBundle()
    {
        SyntheticBundleBuilder.WriteTo(_dir, "a.json",
            SyntheticBundleBuilder.PatientWithObservations("11111111-1111-1111-1111-111111111111", 3, "8480-6"));

        Bundle bundle = _reader.Read(_dir, new CorpusFile("a.json", 0));

        Assert.Equal(Bundle.BundleType.Transaction, bundle.Type);
        Assert.Equal(4, bundle.Entry.Count);
    }

    [Fact]
    public void Census_CountsEveryEntryByResourceType()
    {
        SyntheticBundleBuilder.WriteTo(_dir, "a.json",
            SyntheticBundleBuilder.PatientWithObservations("11111111-1111-1111-1111-111111111111", 3, "8480-6"));
        SyntheticBundleBuilder.WriteTo(_dir, "b.json",
            SyntheticBundleBuilder.PatientWithObservations("22222222-2222-2222-2222-222222222222", 5, "8480-6"));

        ResourceCensus census = _reader.Census(_dir, CorpusDirectoryScanner.Scan(_dir, null));

        Assert.Equal(2, census.CountByResourceType["Patient"]);
        Assert.Equal(8, census.CountByResourceType["Observation"]);
        Assert.Equal(10, census.Total);
    }

    // Review Focus 1 — a non-transaction Bundle must stop the load, naming the file.
    [Fact]
    public void Read_CollectionBundleThrowsNamingTheFile()
    {
        SyntheticBundleBuilder.WriteTo(_dir, "collection.json", new Bundle { Type = Bundle.BundleType.Collection });

        var ex = Assert.Throws<CorpusFileFormatException>(
            () => _reader.Read(_dir, new CorpusFile("collection.json", 0)));

        Assert.Contains("collection.json", ex.Message);
        Assert.Contains("transaction", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Review Focus 1 — a resource that is not a Bundle at all.
    [Fact]
    public void Read_NonBundleResourceThrowsNamingTheFile()
    {
        SyntheticBundleBuilder.WriteTo(_dir, "patient.json", new Patient());

        var ex = Assert.Throws<CorpusFileFormatException>(
            () => _reader.Read(_dir, new CorpusFile("patient.json", 0)));

        Assert.Contains("patient.json", ex.Message);
        Assert.Contains("Patient", ex.Message);
    }

    [Fact]
    public void Read_MalformedJsonThrowsNamingTheFile()
    {
        SyntheticBundleBuilder.WriteRaw(_dir, "broken.json", "{ not json");

        var ex = Assert.Throws<CorpusFileFormatException>(
            () => _reader.Read(_dir, new CorpusFile("broken.json", 0)));

        Assert.Contains("broken.json", ex.Message);
    }

    [Fact]
    public void Read_BundleWithNoEntriesThrows()
    {
        SyntheticBundleBuilder.WriteTo(_dir, "empty.json", new Bundle { Type = Bundle.BundleType.Transaction });

        var ex = Assert.Throws<CorpusFileFormatException>(
            () => _reader.Read(_dir, new CorpusFile("empty.json", 0)));

        Assert.Contains("empty.json", ex.Message);
        Assert.Contains("no entries", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Census must surface the same failure, not silently skip the bad file and undercount.
    [Fact]
    public void Census_PropagatesAFormatFailure()
    {
        SyntheticBundleBuilder.WriteTo(_dir, "a.json",
            SyntheticBundleBuilder.PatientWithObservations("11111111-1111-1111-1111-111111111111", 1, "8480-6"));
        SyntheticBundleBuilder.WriteRaw(_dir, "b.json", "{ not json");

        Assert.Throws<CorpusFileFormatException>(
            () => _reader.Census(_dir, CorpusDirectoryScanner.Scan(_dir, null)));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
```

- [ ] **Step 3: Run the reader tests and verify they fail**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~BundleReaderTest"`
Expected: FAIL — `BundleReader` does not exist.

- [ ] **Step 4: Implement `BundleReader`**

`src/Abm.Pyro.Performance/Loading/BundleReader.cs`:

```csharp
using Abm.Pyro.Performance.Corpus;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Loading;

public class CorpusFileFormatException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// Reads and validates one corpus file. Every failure names the file, because a load of a
/// thousand bundles that says only "deserialisation failed" is a load nobody can fix
/// (spec §16).
/// </summary>
public class BundleReader
{
    private readonly FhirJsonParser _parser = new(new ParserSettings
    {
        AcceptUnknownMembers = true,
        AllowUnrecognizedEnums = true,
        PermissiveParsing = true,
    });

    public Bundle Read(string directoryPath, CorpusFile file)
    {
        string path = Path.Combine(directoryPath, file.FileName);
        Resource resource;

        try
        {
            resource = _parser.Parse<Resource>(File.ReadAllText(path));
        }
        catch (Exception exception)
        {
            throw new CorpusFileFormatException(
                $"Corpus file '{file.FileName}' could not be parsed as a FHIR resource: {exception.Message}",
                exception);
        }

        if (resource is not Bundle bundle)
        {
            throw new CorpusFileFormatException(
                $"Corpus file '{file.FileName}' is a {resource.TypeName}, not a Bundle. " +
                "Every corpus file must be a FHIR Bundle of type 'transaction'.");
        }

        if (bundle.Type != Bundle.BundleType.Transaction)
        {
            throw new CorpusFileFormatException(
                $"Corpus file '{file.FileName}' is a Bundle of type '{bundle.Type}', not 'transaction'. " +
                "The loader drives Pyro's transaction pipeline and cannot load any other bundle type.");
        }

        if (bundle.Entry.Count == 0)
        {
            throw new CorpusFileFormatException(
                $"Corpus file '{file.FileName}' is a transaction Bundle with no entries.");
        }

        return bundle;
    }

    public ResourceCensus Census(string directoryPath, IReadOnlyList<CorpusFile> files)
    {
        Dictionary<string, int> counts = new(StringComparer.Ordinal);

        foreach (CorpusFile file in files)
        {
            foreach (Bundle.EntryComponent entry in Read(directoryPath, file).Entry)
            {
                if (entry.Resource is null)
                {
                    throw new CorpusFileFormatException(
                        $"Corpus file '{file.FileName}' has a transaction entry with no resource.");
                }

                string typeName = entry.Resource.TypeName;
                counts[typeName] = counts.GetValueOrDefault(typeName) + 1;
            }
        }

        return new ResourceCensus(counts);
    }
}
```

- [ ] **Step 5: Run the reader tests and verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~BundleReaderTest"`
Expected: PASS — 8 tests.

- [ ] **Step 6: Commit**

```bash
git add src/Abm.Pyro.Performance/Loading src/Abm.Pyro.Performance.Test
git commit -m "feat(perf): read and census Synthea transaction bundles

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: Performance host — container, migrations, DI graph, tenant scope

Everything from here needs the real Pyro DI graph against a real SQL Server. This task builds that and nothing else, so a failure here is unambiguous.

**Files:**
- Create: `src/Abm.Pyro.Performance/Hosting/PerformanceWebApplicationFactory.cs`
- Create: `src/Abm.Pyro.Performance/Hosting/PerformanceHost.cs`
- Create: `src/Abm.Pyro.Performance/Hosting/TenantScope.cs`
- Test: `src/Abm.Pyro.Performance.Test/Integration/PerformanceHostFixture.cs`
- Test: `src/Abm.Pyro.Performance.Test/Integration/PerformanceHostTest.cs`

**Interfaces:**
- Consumes: `PerformanceSettings` (Task 1).
- Produces:
  - `class PerformanceWebApplicationFactory(string sqlConnectionString) : WebApplicationFactory<Program>` — note `Program` here is `Abm.Pyro.Api`'s, reached through the project reference
  - `class PerformanceHost : IAsyncDisposable` with `static Task<PerformanceHost> StartAsync(PerformanceSettings settings)`, `string ConnectionString { get; }`, `IServiceProvider Services { get; }`, `Task MigrateAsync()`
  - `class TenantScope : IDisposable` with `static TenantScope Create(IServiceProvider rootServices, string tenantCode)`, `IServiceProvider Services { get; }`

- [ ] **Step 1: Implement the factory**

`src/Abm.Pyro.Performance/Hosting/PerformanceWebApplicationFactory.cs`:

```csharp
using Abm.Pyro.Application.HostedServiceSupport;
using Abm.Pyro.Application.OnStartupService;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Abm.Pyro.Performance.Hosting;

/// <summary>
/// Hosts the real Pyro DI graph. Mirrors Abm.Pyro.Api.Test's PyroWebApplicationFactory, with two
/// differences: the clock is NOT swapped (the harness wants the production clock, because
/// LastUpdatedUtc is part of what it measures paging against), and NotificationManager is removed
/// because a one-second recurring subscription service would add background I/O to every
/// measurement.
/// </summary>
public class PerformanceWebApplicationFactory(string sqlConnectionString)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PyroDb"] = sqlConnectionString,
                ["ServiceBaseUrl:Url"] = "https://localhost",
                ["spring:cloud:config:enabled"] = "false",
                ["spring:cloud:config:failFast"] = "false",
                ["Serilog:MinimumLevel:Default"] = "Warning",
            });
        });

        builder.ConfigureServices(services =>
        {
            RemoveHostedService(services, typeof(AppStartupServiceManager<DatabaseVersionValidationOnStartupService>));
            RemoveHostedService(services, typeof(NotificationManager));
        });
    }

    private static void RemoveHostedService(IServiceCollection services, Type implementationType)
    {
        ServiceDescriptor? descriptor = services.FirstOrDefault(d => d.ImplementationType == implementationType);

        if (descriptor is not null)
        {
            services.Remove(descriptor);
        }
    }
}
```

> **Note for the implementer:** `NotificationManager`'s namespace is not `Abm.Pyro.Application.OnStartupService`. Find it with `grep -rn "class NotificationManager" src --include=*.cs` and add the correct `using`. If it is registered by a wrapper type rather than directly, match on that wrapper the same way `DatabaseVersionValidationOnStartupService` is matched. If removing it proves awkward, leave it in and note it in the commit message — it is a measurement-noise concern, not a correctness one.

- [ ] **Step 2: Implement the tenant scope**

`src/Abm.Pyro.Performance/Hosting/TenantScope.cs`:

```csharp
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Abm.Pyro.Performance.Hosting;

/// <summary>
/// A DI scope with the tenant already set. Pyro normally resolves the tenant from the
/// {tenant} route segment; outside HTTP there is no route, so the harness sets it explicitly
/// via ITenantService.SetScopedTenant before anything resolves a DbContext.
/// </summary>
public sealed class TenantScope : IDisposable
{
    private readonly IServiceScope _scope;

    private TenantScope(IServiceScope scope) => _scope = scope;

    public IServiceProvider Services => _scope.ServiceProvider;

    public static TenantScope Create(IServiceProvider rootServices, string tenantCode)
    {
        IServiceScope scope = rootServices.CreateScope();
        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();

        Tenant tenant = tenantService.GetTenantList()
                            .FirstOrDefault(t => string.Equals(t.Code, tenantCode, StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidOperationException(
                            $"Tenant '{tenantCode}' is not configured. Configured tenants: " +
                            string.Join(", ", tenantService.GetTenantList().Select(t => t.Code)));

        tenantService.SetScopedTenant(tenant);
        return new TenantScope(scope);
    }

    public void Dispose() => _scope.Dispose();
}
```

- [ ] **Step 3: Implement the host**

`src/Abm.Pyro.Performance/Hosting/PerformanceHost.cs`:

```csharp
using Abm.Pyro.Performance.Configuration;
using Abm.Pyro.Repository;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Hosting;

/// <summary>
/// Owns the SQL Server container and the Pyro DI host for the life of one command.
/// The container is capped at PerformanceSettings.ContainerMemoryLimitBytes, because buffer
/// pressure is a dial rather than a data volume (spec §5.2), and gets a named volume for
/// backups so a .bak never lands on a path git can reach (spec §5.3, §10.2).
/// </summary>
public sealed class PerformanceHost : IAsyncDisposable
{
    private readonly MsSqlContainer _container;
    private PerformanceWebApplicationFactory? _factory;

    private PerformanceHost(MsSqlContainer container, PerformanceSettings settings)
    {
        _container = container;
        Settings = settings;
    }

    public PerformanceSettings Settings { get; }

    public string ConnectionString { get; private set; } = default!;

    public IServiceProvider Services =>
        (_factory ?? throw new InvalidOperationException("Host not started.")).Services;

    public static async Task<PerformanceHost> StartAsync(PerformanceSettings settings)
    {
        MsSqlContainer container = new MsSqlBuilder(image: "mcr.microsoft.com/mssql/server:2022-latest")
            .WithName("pyro-perf-sql")
            .WithVolumeMount(settings.BackupVolumeName, "/var/opt/mssql/backup")
            .WithCreateParameterModifier(p => p.HostConfig.Memory = settings.ContainerMemoryLimitBytes)
            .WithReuse(true)
            .Build();

        await container.StartAsync();

        var host = new PerformanceHost(container, settings)
        {
            ConnectionString = container.GetConnectionString(),
        };

        await host.MigrateAsync();
        host._factory = new PerformanceWebApplicationFactory(host.ConnectionString);

        // Touch the DI graph so startup services run before the first measurement.
        _ = host._factory.Services;

        return host;
    }

    /// <summary>
    /// Applies EF migrations. Called at startup and again after a restore, because a .bak
    /// carries the schema as of load time (spec §20.2, Review Focus 4).
    /// </summary>
    public async Task MigrateAsync()
    {
        var options = new DbContextOptionsBuilder<PyroDbContext>()
            .UseSqlServer(ConnectionString, o => o.UseNetTopologySuite())
            .Options;

        await using var context = new PyroDbContext(options);
        await context.Database.MigrateAsync();
    }

    public async Task<bool> HasPendingMigrationsAsync()
    {
        var options = new DbContextOptionsBuilder<PyroDbContext>()
            .UseSqlServer(ConnectionString, o => o.UseNetTopologySuite())
            .Options;

        await using var context = new PyroDbContext(options);
        return (await context.Database.GetPendingMigrationsAsync()).Any();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        // The container is reused across runs by design (spec §9: long-lived by default), so it
        // is stopped, not removed. `docker rm -f pyro-perf-sql` is the manual teardown.
        await _container.StopAsync();
    }
}
```

> **Note for the implementer:** `WithCreateParameterModifier` and `WithReuse` exist in Testcontainers 4.x but the exact member names have moved between versions. If either does not compile, set the memory limit with `.WithCreateParameterModifier(p => p.HostConfig.Memory = ...)`'s nearest equivalent in the installed version and, failing that, drop the reuse flag and keep the memory cap — the cap is required by spec §5.2, reuse is only a convenience.

- [ ] **Step 4: Write the failing host test and fixture**

`src/Abm.Pyro.Performance.Test/Integration/PerformanceHostFixture.cs`:

```csharp
using Abm.Pyro.Performance.Configuration;
using Abm.Pyro.Performance.Hosting;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Test.Integration;

/// <summary>
/// One container and one Pyro host shared by every integration test in this project.
/// Requires Docker.
/// </summary>
public class PerformanceHostFixture : IAsyncLifetime
{
    public PerformanceHost Host { get; private set; } = default!;

    public PerformanceSettings Settings { get; } = new()
    {
        DatabaseName = "PyroPerfTest",
        BackupVolumeName = "pyro-perf-test-backup",
        ContainerMemoryLimitBytes = 4L * 1024 * 1024 * 1024,
        TenantCode = "Pyro",
        TenantUrlCode = "pyro",
    };

    public async Task InitializeAsync() => Host = await PerformanceHost.StartAsync(Settings);

    public async Task DisposeAsync() => await Host.DisposeAsync();
}

[CollectionDefinition(nameof(PerformanceHostCollection))]
public class PerformanceHostCollection : ICollectionFixture<PerformanceHostFixture>;
```

`src/Abm.Pyro.Performance.Test/Integration/PerformanceHostTest.cs`:

```csharp
using Abm.Pyro.Application.Dispatcher;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Performance.Hosting;
using Abm.Pyro.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Test.Integration;

[Collection(nameof(PerformanceHostCollection))]
public class PerformanceHostTest(PerformanceHostFixture fixture)
{
    [Fact]
    public async Task MigrationsAreApplied()
    {
        Assert.False(await fixture.Host.HasPendingMigrationsAsync());
    }

    [Fact]
    public void TenantScope_SetsTheScopedTenant()
    {
        using TenantScope scope = TenantScope.Create(fixture.Host.Services, fixture.Settings.TenantCode);

        var tenantService = scope.Services.GetRequiredService<ITenantService>();

        Assert.Equal("Pyro", tenantService.GetScopedTenantCode());
    }

    [Fact]
    public void TenantScope_UnknownTenantThrowsListingTheConfiguredOnes()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => TenantScope.Create(fixture.Host.Services, "NotATenant"));

        Assert.Contains("NotATenant", ex.Message);
        Assert.Contains("Pyro", ex.Message);
    }

    [Fact]
    public void TenantScope_ResolvesTheRequestDispatcher()
    {
        using TenantScope scope = TenantScope.Create(fixture.Host.Services, fixture.Settings.TenantCode);

        Assert.NotNull(scope.Services.GetRequiredService<IRequestDispatcher>());
    }

    [Fact]
    public async Task TenantScope_ResolvesAUsableDbContext()
    {
        using TenantScope scope = TenantScope.Create(fixture.Host.Services, fixture.Settings.TenantCode);

        var context = scope.Services.GetRequiredService<PyroDbContext>();

        // SearchParameterStore is seeded by a startup service, so a non-zero count also proves
        // the startup services ran.
        Assert.True(await context.SearchParameterStore.AnyAsync());
    }
}
```

> **Note for the implementer:** `PyroDbContext` may not be registered directly — `IPyroDbContextFactory` is the documented path (`CLAUDE.md`). If `GetRequiredService<PyroDbContext>()` throws, resolve `IPyroDbContextFactory` and call its factory method instead, and use that same approach everywhere later tasks need a context.

- [ ] **Step 5: Run the host tests and verify they fail**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~PerformanceHostTest"`
Expected: FAIL — types do not exist, or Docker is not running (start Docker Desktop first).

- [ ] **Step 6: Make the host tests pass**

Fix whatever the two implementer notes above surface — the `NotificationManager` namespace, the Testcontainers member names, and the `PyroDbContext` versus `IPyroDbContextFactory` resolution. Re-run until green.

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~PerformanceHostTest"`
Expected: PASS — 5 tests.

- [ ] **Step 7: Commit**

```bash
git add src/Abm.Pyro.Performance/Hosting src/Abm.Pyro.Performance.Test/Integration
git commit -m "feat(perf): host the real Pyro DI graph against a capped SQL container

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: Load preconditions

Spec §9 requires three checks before the first bundle is dispatched, so a misconfigured run fails in seconds rather than four hundred bundles in.

**Files:**
- Create: `src/Abm.Pyro.Performance/Loading/LoadPreconditions.cs`
- Test: `src/Abm.Pyro.Performance.Test/Integration/LoadPreconditionsTest.cs`

**Interfaces:**
- Consumes: `TenantScope` (Task 3), `ResourceCensus` (Task 1), `BundleReader` (Task 2).
- Produces:
  - `class LoadPreconditions(IServiceProvider scopedServices)` with `Task AssertDatabaseIsEmptyOrForcedAsync(bool force)`, `Task AssertProfileValidationIsOffAsync()`, `void AssertEndpointPolicyAllows(ResourceCensus census)`
  - `class LoadPreconditionException : Exception`

- [ ] **Step 1: Write the failing precondition tests**

`src/Abm.Pyro.Performance.Test/Integration/LoadPreconditionsTest.cs`:

```csharp
using Abm.Pyro.Performance.Corpus;
using Abm.Pyro.Performance.Hosting;
using Abm.Pyro.Performance.Loading;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Test.Integration;

[Collection(nameof(PerformanceHostCollection))]
public class LoadPreconditionsTest(PerformanceHostFixture fixture)
{
    private LoadPreconditions Create(TenantScope scope) => new(scope.Services);

    [Fact]
    public async Task EmptyDatabasePassesWithoutForce()
    {
        using TenantScope scope = TenantScope.Create(fixture.Host.Services, fixture.Settings.TenantCode);

        await Create(scope).AssertDatabaseIsEmptyOrForcedAsync(force: false);
    }

    [Fact]
    public async Task ProfileValidationIsOffByDefault()
    {
        using TenantScope scope = TenantScope.Create(fixture.Host.Services, fixture.Settings.TenantCode);

        // Asserts, not configures — the EF migration seeds ValidateOnCreate/ValidateOnUpdate false.
        await Create(scope).AssertProfileValidationIsOffAsync();
    }

    [Fact]
    public void EndpointPolicyAllowsEveryTypeInTheDefaultCensus()
    {
        using TenantScope scope = TenantScope.Create(fixture.Host.Services, fixture.Settings.TenantCode);

        var census = new ResourceCensus(new Dictionary<string, int>
        {
            ["Patient"] = 1,
            ["Observation"] = 1,
            ["Encounter"] = 1,
        });

        Create(scope).AssertEndpointPolicyAllows(census);
    }

    [Fact]
    public void EndpointPolicyRefusesATypeUnderAReadOnlyPolicy()
    {
        using TenantScope scope = TenantScope.Create(fixture.Host.Services, fixture.Settings.TenantCode);

        // appsettings.json enforces the ReadAndSearch policy on the Basic endpoint, which has
        // AllowCreate false — so a corpus containing Basic must be refused up front.
        var census = new ResourceCensus(new Dictionary<string, int> { ["Basic"] = 1 });

        var ex = Assert.Throws<LoadPreconditionException>(
            () => Create(scope).AssertEndpointPolicyAllows(census));

        Assert.Contains("Basic", ex.Message);
    }

    [Fact]
    public void EndpointPolicyRefusesAnUnknownResourceType()
    {
        using TenantScope scope = TenantScope.Create(fixture.Host.Services, fixture.Settings.TenantCode);

        var census = new ResourceCensus(new Dictionary<string, int> { ["NotAResource"] = 1 });

        var ex = Assert.Throws<LoadPreconditionException>(
            () => Create(scope).AssertEndpointPolicyAllows(census));

        Assert.Contains("NotAResource", ex.Message);
    }
}
```

> **Note for the implementer:** the non-empty-database refusal is tested in Task 5, after the loader can actually populate the database. Add this test there:
> ```csharp
> [Fact]
> public async Task NonEmptyDatabaseIsRefusedWithoutForce()  // in CorpusLoaderTest
> {
>     // after a successful load of one bundle
>     var ex = await Assert.ThrowsAsync<LoadPreconditionException>(
>         () => preconditions.AssertDatabaseIsEmptyOrForcedAsync(force: false));
>     Assert.Contains("--force", ex.Message);
> }
> ```

- [ ] **Step 2: Run the precondition tests and verify they fail**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~LoadPreconditionsTest"`
Expected: FAIL — `LoadPreconditions` does not exist.

- [ ] **Step 3: Implement `LoadPreconditions`**

`src/Abm.Pyro.Performance/Loading/LoadPreconditions.cs`:

```csharp
using Abm.Pyro.Application.Cache;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.ServiceSettings;
using Abm.Pyro.Performance.Corpus;
using Abm.Pyro.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Loading;

public class LoadPreconditionException(string message) : Exception(message);

/// <summary>
/// Spec §9's three checks, all performed before the first bundle is dispatched.
/// </summary>
public class LoadPreconditions(IServiceProvider scopedServices)
{
    public async Task AssertDatabaseIsEmptyOrForcedAsync(bool force)
    {
        var context = scopedServices.GetRequiredService<PyroDbContext>();
        int existing = await context.ResourceStore.CountAsync();

        if (existing > 0 && !force)
        {
            throw new LoadPreconditionException(
                $"The database already holds {existing:N0} ResourceStore rows. " +
                "Loading over an existing corpus is refused; pass --force to reload destructively, " +
                "or use 'reset' to restore the snapshot.");
        }
    }

    /// <summary>
    /// An assertion, not a configuration step: FhirValidationSettings.SeedDefaultSettings()
    /// already seeds ValidateOnCreate and ValidateOnUpdate false. If a future migration or an
    /// admin change turns validation on, a 500k-resource load becomes impractical, so the
    /// harness refuses rather than running for hours.
    /// </summary>
    public async Task AssertProfileValidationIsOffAsync()
    {
        var cache = scopedServices.GetRequiredService<IServiceSettingsCache>();
        FhirValidationSettings settings = await cache.GetFhirValidationSettings();

        if (settings.ValidateOnCreate || settings.ValidateOnUpdate)
        {
            throw new LoadPreconditionException(
                "FHIR profile validation is enabled for this tenant " +
                $"(ValidateOnCreate={settings.ValidateOnCreate}, ValidateOnUpdate={settings.ValidateOnUpdate}). " +
                "The harness requires it off: a 500k-resource load is impractical with validation on, " +
                "and every write-cost figure this harness reports excludes validation cost. " +
                "Turn it off via the admin endpoint before loading.");
        }
    }

    public void AssertEndpointPolicyAllows(ResourceCensus census)
    {
        List<string> unknown = [];
        List<string> forbidden = [];

        foreach (string typeName in census.CountByResourceType.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (!Enum.TryParse(typeName, ignoreCase: false, out FhirResourceTypeId resourceType))
            {
                unknown.Add(typeName);
                continue;
            }

            if (!IsCreateAllowed(resourceType))
            {
                forbidden.Add(typeName);
            }
        }

        if (unknown.Count > 0)
        {
            throw new LoadPreconditionException(
                $"The corpus contains resource types Pyro does not recognise: {string.Join(", ", unknown)}. " +
                "Check the corpus directory is a FHIR R4 export.");
        }

        if (forbidden.Count > 0)
        {
            throw new LoadPreconditionException(
                $"The tenant's ResourceEndpointPolicies forbid create for: {string.Join(", ", forbidden)}. " +
                "Every resource type present in the corpus must be creatable. Grant the AllowAll policy " +
                "to these endpoints in appsettings.json, or point the harness at a corpus without them.");
        }
    }

    private bool IsCreateAllowed(FhirResourceTypeId resourceType) =>
        throw new NotImplementedException("Implemented in Step 4.");
}
```

- [ ] **Step 4: Wire the endpoint-policy lookup to the real policy service**

Find the service that `ValidateAndPrimeResourceEndpointPoliciesOnStartupService` primes and that the validators read:

```bash
grep -rn "AllowCreate" src/Abm.Pyro.Application src/Abm.Pyro.Domain --include=*.cs | head -20
grep -rln "ResourceEndpointPolicy" src/Abm.Pyro.Application src/Abm.Pyro.Domain --include=*.cs
```

Replace `IsCreateAllowed` with a call into that service, resolved from `scopedServices`. It will look like this, with the real interface and member names substituted:

```csharp
private bool IsCreateAllowed(FhirResourceTypeId resourceType)
{
    var policyService = scopedServices.GetRequiredService<IResourceEndpointPolicyService>();
    return policyService.GetPolicy(resourceType).AllowCreate;
}
```

Transaction bundles reach the create path per entry, so `AllowCreate` per resource type is the right gate — not `AllowBaseTransaction`, which governs the bundle endpoint itself and is granted by the `AllowAll` default policy.

- [ ] **Step 5: Run the precondition tests and verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~LoadPreconditionsTest"`
Expected: PASS — 5 tests.

- [ ] **Step 6: Commit**

```bash
git add src/Abm.Pyro.Performance/Loading src/Abm.Pyro.Performance.Test/Integration
git commit -m "feat(perf): assert load preconditions before the first bundle

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---
### Task 5: The loader, throughput reporting, table sizes, manifest persistence, and `load`

The heart of the harness. A failing entry mid-bundle must abort the load — Review Focus 2 — because the bundle rolls back and the running count would otherwise overstate what the database holds.

**Files:**
- Create: `src/Abm.Pyro.Performance/Loading/LoadProgress.cs`
- Create: `src/Abm.Pyro.Performance/Loading/CorpusLoader.cs`
- Create: `src/Abm.Pyro.Performance/Loading/ManifestStore.cs`
- Create: `src/Abm.Pyro.Performance/Loading/TableSizeReporter.cs`
- Create: `src/Abm.Pyro.Performance/Cli/LoadCommand.cs`
- Modify: `src/Abm.Pyro.Performance/Program.cs`
- Test: `src/Abm.Pyro.Performance.Test/Integration/CorpusLoaderTest.cs`

**Interfaces:**
- Consumes: `BundleReader`, `ResourceCensus`, `CorpusManifest`, `CorpusIdentity`, `TenantScope`, `LoadPreconditions`.
- Produces:
  - `record LoadProgress(int FilesLoaded, int ResourcesLoaded, TimeSpan Elapsed)` with `double ResourcesPerSecond`
  - `class CorpusLoader(IServiceProvider rootServices, PerformanceSettings settings, BundleReader reader)` with `Task<CorpusManifest> LoadAsync(string corpusDirectory, int? maxFiles, bool force, IProgress<LoadProgress>? progress, CancellationToken ct)`
  - `class CorpusLoadException : Exception`
  - `class ManifestStore(string connectionString)` with `Task EnsureTableAsync()`, `Task WriteAsync(CorpusManifest)`, `Task<CorpusManifest?> ReadAsync()`
  - `record TableSize(string TableName, long Rows, long ReservedKb, long DataKb, long IndexKb)`
  - `class TableSizeReporter(string connectionString)` with `Task<IReadOnlyList<TableSize>> MeasureAsync()`

- [ ] **Step 1: Write the failing loader tests**

`src/Abm.Pyro.Performance.Test/Integration/CorpusLoaderTest.cs`:

```csharp
using Abm.Pyro.Performance.Corpus;
using Abm.Pyro.Performance.Hosting;
using Abm.Pyro.Performance.Loading;
using Abm.Pyro.Performance.Test.Support;
using Abm.Pyro.Repository;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Test.Integration;

[Collection(nameof(PerformanceHostCollection))]
public class CorpusLoaderTest(PerformanceHostFixture fixture) : IAsyncLifetime
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pyro-perf-load").FullName;

    public Task InitializeAsync() => TruncateCorpusTablesAsync(fixture);

    public Task DisposeAsync()
    {
        Directory.Delete(_dir, recursive: true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears only the resource and index tables, exactly as Abm.Pyro.Api.Test's Respawn
    /// configuration does. SearchParameterStore, ServiceBaseUrl and ServiceSetting must survive.
    /// </summary>
    internal static async Task TruncateCorpusTablesAsync(PerformanceHostFixture fixture)
    {
        using TenantScope scope = TenantScope.Create(fixture.Host.Services, fixture.Settings.TenantCode);
        var context = scope.Services.GetRequiredService<PyroDbContext>();

        foreach (string table in new[]
                 {
                     "IndexString", "IndexReference", "IndexDateTime", "IndexQuantity",
                     "IndexToken", "IndexUri", "IndexPosition", "ResourceStore",
                 })
        {
            await context.Database.ExecuteSqlRawAsync($"DELETE FROM [{table}]");
        }
    }

    private CorpusLoader CreateLoader() =>
        new(fixture.Host.Services, fixture.Settings, new BundleReader());

    [Fact]
    public async Task LoadAsync_StoresEveryResourceInTheCorpus()
    {
        SyntheticBundleBuilder.WriteTo(_dir, "a.json",
            SyntheticBundleBuilder.PatientWithObservations("11111111-1111-1111-1111-111111111111", 3, "8480-6"));
        SyntheticBundleBuilder.WriteTo(_dir, "b.json",
            SyntheticBundleBuilder.PatientWithObservations("22222222-2222-2222-2222-222222222222", 5, "8462-4"));

        CorpusManifest manifest = await CreateLoader()
            .LoadAsync(_dir, maxFiles: null, force: false, progress: null, CancellationToken.None);

        Assert.Equal(10, manifest.Census.Total);
        Assert.Equal(10, manifest.ResourceStoreRowCount);
        Assert.Equal(2, manifest.Census.CountByResourceType["Patient"]);
        Assert.Equal(8, manifest.Census.CountByResourceType["Observation"]);
    }

    [Fact]
    public async Task LoadAsync_RunsTheRealIndexSetters()
    {
        SyntheticBundleBuilder.WriteTo(_dir, "a.json",
            SyntheticBundleBuilder.PatientWithObservations("11111111-1111-1111-1111-111111111111", 3, "8480-6"));

        await CreateLoader().LoadAsync(_dir, null, false, null, CancellationToken.None);

        using TenantScope scope = TenantScope.Create(fixture.Host.Services, fixture.Settings.TenantCode);
        var context = scope.Services.GetRequiredService<PyroDbContext>();

        // The Patient's family name indexes to IndexString, the Observation code to IndexToken,
        // the effective date to IndexDateTime, the value to IndexQuantity, and subject to
        // IndexReference. All five prove the production setters ran.
        Assert.True(await context.IndexString.AnyAsync());
        Assert.True(await context.Set<Abm.Pyro.Domain.Model.IndexToken>().AnyAsync());
        Assert.True(await context.Set<Abm.Pyro.Domain.Model.IndexDateTime>().AnyAsync());
        Assert.True(await context.Set<Abm.Pyro.Domain.Model.IndexQuantity>().AnyAsync());
        Assert.True(await context.IndexReference.AnyAsync());
    }

    [Fact]
    public async Task LoadAsync_ResolvesUrnUuidReferencesToStoredIds()
    {
        SyntheticBundleBuilder.WriteTo(_dir, "a.json",
            SyntheticBundleBuilder.PatientWithObservations("11111111-1111-1111-1111-111111111111", 2, "8480-6"));

        await CreateLoader().LoadAsync(_dir, null, false, null, CancellationToken.None);

        using TenantScope scope = TenantScope.Create(fixture.Host.Services, fixture.Settings.TenantCode);
        var context = scope.Services.GetRequiredService<PyroDbContext>();

        // Every Observation's subject must be an IndexReference row pointing at Patient, not a
        // dangling urn:uuid. This is the transaction pipeline's reference rewriting.
        List<Abm.Pyro.Domain.Model.IndexReference> references = await context.IndexReference.ToListAsync();

        Assert.NotEmpty(references);
        Assert.Contains(references, r => r.ResourceType == Abm.Pyro.Domain.Enums.FhirResourceTypeId.Patient);
        Assert.DoesNotContain(references, r => r.ResourceId.StartsWith("urn:uuid", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_MaxFilesLimitsTheLoad()
    {
        SyntheticBundleBuilder.WriteTo(_dir, "a.json",
            SyntheticBundleBuilder.PatientWithObservations("11111111-1111-1111-1111-111111111111", 1, "8480-6"));
        SyntheticBundleBuilder.WriteTo(_dir, "b.json",
            SyntheticBundleBuilder.PatientWithObservations("22222222-2222-2222-2222-222222222222", 1, "8480-6"));

        CorpusManifest manifest = await CreateLoader()
            .LoadAsync(_dir, maxFiles: 1, force: false, null, CancellationToken.None);

        Assert.Equal(2, manifest.Census.Total);
        Assert.Equal(1, manifest.MaxFiles);
        Assert.Equal("a.json", manifest.Files.Single().FileName);
    }

    [Fact]
    public async Task LoadAsync_WritesTheManifestIntoTheDatabase()
    {
        SyntheticBundleBuilder.WriteTo(_dir, "a.json",
            SyntheticBundleBuilder.PatientWithObservations("11111111-1111-1111-1111-111111111111", 1, "8480-6"));

        CorpusManifest written = await CreateLoader().LoadAsync(_dir, null, false, null, CancellationToken.None);

        var store = new ManifestStore(fixture.Host.ConnectionString);
        CorpusManifest? read = await store.ReadAsync();

        Assert.NotNull(read);
        Assert.Equal(written.CorpusIdentityHash, read.CorpusIdentityHash);
        Assert.Equal(written.ResourceStoreRowCount, read.ResourceStoreRowCount);
    }

    [Fact]
    public async Task LoadAsync_ReportsProgressAndThroughput()
    {
        SyntheticBundleBuilder.WriteTo(_dir, "a.json",
            SyntheticBundleBuilder.PatientWithObservations("11111111-1111-1111-1111-111111111111", 1, "8480-6"));

        List<LoadProgress> reports = [];
        var progress = new Progress<LoadProgress>(reports.Add);

        await CreateLoader().LoadAsync(_dir, null, false, progress, CancellationToken.None);

        // Progress<T> marshals asynchronously; give it a moment to drain.
        await Task.Delay(100);

        Assert.NotEmpty(reports);
        Assert.All(reports, r => Assert.True(r.ResourcesPerSecond >= 0));
    }

    // Review Focus 2 — a bundle whose entries cannot all commit must abort the load.
    [Fact]
    public async Task LoadAsync_AbortsWhenABundleFails()
    {
        SyntheticBundleBuilder.WriteTo(_dir, "a.json",
            SyntheticBundleBuilder.PatientWithObservations("11111111-1111-1111-1111-111111111111", 1, "8480-6"));

        // An Observation whose subject points at a urn:uuid that no entry in the bundle defines.
        // The transaction cannot resolve the reference, so the whole bundle fails.
        var broken = new Bundle { Type = Bundle.BundleType.Transaction };
        broken.Entry.Add(new Bundle.EntryComponent
        {
            FullUrl = "urn:uuid:33333333-3333-3333-3333-333333333333",
            Resource = new Observation
            {
                Status = ObservationStatus.Final,
                Code = new CodeableConcept("http://loinc.org", "8480-6"),
                Subject = new ResourceReference("urn:uuid:99999999-9999-9999-9999-999999999999"),
            },
            Request = new Bundle.RequestComponent { Method = Bundle.HTTPVerb.POST, Url = "Observation" },
        });
        SyntheticBundleBuilder.WriteTo(_dir, "b.json", broken);

        var ex = await Assert.ThrowsAsync<CorpusLoadException>(
            () => CreateLoader().LoadAsync(_dir, null, false, null, CancellationToken.None));

        Assert.Contains("b.json", ex.Message);
    }

    // The deferred test from Task 4.
    [Fact]
    public async Task LoadAsync_RefusesToLoadOverAnExistingCorpusWithoutForce()
    {
        SyntheticBundleBuilder.WriteTo(_dir, "a.json",
            SyntheticBundleBuilder.PatientWithObservations("11111111-1111-1111-1111-111111111111", 1, "8480-6"));

        await CreateLoader().LoadAsync(_dir, null, false, null, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<LoadPreconditionException>(
            () => CreateLoader().LoadAsync(_dir, null, force: false, null, CancellationToken.None));

        Assert.Contains("--force", ex.Message);
    }

    [Fact]
    public async Task LoadAsync_ForceClearsAndReloads()
    {
        SyntheticBundleBuilder.WriteTo(_dir, "a.json",
            SyntheticBundleBuilder.PatientWithObservations("11111111-1111-1111-1111-111111111111", 1, "8480-6"));

        await CreateLoader().LoadAsync(_dir, null, false, null, CancellationToken.None);
        CorpusManifest second = await CreateLoader()
            .LoadAsync(_dir, null, force: true, null, CancellationToken.None);

        Assert.Equal(2, second.ResourceStoreRowCount);
    }
}
```

> **Note on the `AbortsWhenABundleFails` test:** if Pyro's transaction pipeline tolerates an unresolvable `urn:uuid` reference and stores it as a literal, this test will not fail the bundle. In that case substitute a different guaranteed failure — the simplest is an entry with `Request.Url = "NotAResourceType"`. Verify which one actually fails by running the test and reading the exception, then keep whichever genuinely aborts. Do not weaken the test to match the implementation: the behaviour being pinned is "a failed bundle aborts the load", and some input must demonstrate it.

- [ ] **Step 2: Run the loader tests and verify they fail**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~CorpusLoaderTest"`
Expected: FAIL — `CorpusLoader`, `LoadProgress`, `ManifestStore` do not exist.

- [ ] **Step 3: Implement `LoadProgress`**

`src/Abm.Pyro.Performance/Loading/LoadProgress.cs`:

```csharp
namespace Abm.Pyro.Performance.Loading;

public record LoadProgress(int FilesLoaded, int FilesTotal, int ResourcesLoaded, TimeSpan Elapsed)
{
    public double ResourcesPerSecond =>
        Elapsed.TotalSeconds <= 0 ? 0 : ResourcesLoaded / Elapsed.TotalSeconds;

    public override string ToString() =>
        $"{FilesLoaded:N0}/{FilesTotal:N0} files, {ResourcesLoaded:N0} resources, " +
        $"{ResourcesPerSecond:N0} res/s, {Elapsed:hh\\:mm\\:ss} elapsed";
}
```

- [ ] **Step 4: Implement `ManifestStore`**

`src/Abm.Pyro.Performance/Loading/ManifestStore.cs`:

```csharp
using Abm.Pyro.Performance.Corpus;
using Microsoft.Data.SqlClient;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Loading;

/// <summary>
/// The manifest, stored inside the performance database so it travels with the .bak and a
/// restored database self-identifies (spec §10.4). Created by raw SQL, never by an EF
/// migration; EF has no entity for it and therefore ignores it.
/// </summary>
public class ManifestStore(string connectionString)
{
    public const string TableName = "PerfCorpusManifest";

    public async Task EnsureTableAsync()
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF OBJECT_ID('{TableName}', 'U') IS NULL
            CREATE TABLE [{TableName}] (
                [Id]   INT           NOT NULL PRIMARY KEY,
                [Json] NVARCHAR(MAX) NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task WriteAsync(CorpusManifest manifest)
    {
        await EnsureTableAsync();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            DELETE FROM [{TableName}];
            INSERT INTO [{TableName}] ([Id], [Json]) VALUES (1, @json);
            """;
        command.Parameters.AddWithValue("@json", CorpusManifest.ToJson(manifest));
        await command.ExecuteNonQueryAsync();
    }

    public async Task<CorpusManifest?> ReadAsync()
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF OBJECT_ID('{TableName}', 'U') IS NULL SELECT CAST(NULL AS NVARCHAR(MAX))
            ELSE SELECT [Json] FROM [{TableName}] WHERE [Id] = 1;
            """;

        object? result = await command.ExecuteScalarAsync();
        return result is string json ? CorpusManifest.FromJson(json) : null;
    }
}
```

- [ ] **Step 5: Implement `TableSizeReporter`**

`src/Abm.Pyro.Performance/Loading/TableSizeReporter.cs`:

```csharp
using Microsoft.Data.SqlClient;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Loading;

public record TableSize(string TableName, long Rows, long ReservedKb, long DataKb, long IndexKb);

/// <summary>
/// Spec §13's first deliverable: measured per-table sizes, replacing the estimates the
/// superseded design carried. Reads the catalogue views rather than sp_spaceused, so the
/// result is one result set rather than one per table.
/// </summary>
public class TableSizeReporter(string connectionString)
{
    private const string Sql = """
        SELECT
            t.name                                             AS TableName,
            SUM(CASE WHEN i.index_id < 2 THEN p.rows ELSE 0 END) AS [Rows],
            SUM(a.total_pages) * 8                             AS ReservedKb,
            SUM(a.data_pages)  * 8                             AS DataKb,
            (SUM(a.used_pages) - SUM(a.data_pages)) * 8         AS IndexKb
        FROM sys.tables t
        INNER JOIN sys.indexes i      ON t.object_id = i.object_id
        INNER JOIN sys.partitions p   ON i.object_id = p.object_id AND i.index_id = p.index_id
        INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
        WHERE t.is_ms_shipped = 0
        GROUP BY t.name
        ORDER BY ReservedKb DESC;
        """;

    public async Task<IReadOnlyList<TableSize>> MeasureAsync()
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = Sql;

        List<TableSize> sizes = [];
        await using SqlDataReader reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            sizes.Add(new TableSize(
                reader.GetString(0),
                Convert.ToInt64(reader.GetValue(1)),
                Convert.ToInt64(reader.GetValue(2)),
                Convert.ToInt64(reader.GetValue(3)),
                Convert.ToInt64(reader.GetValue(4))));
        }

        return sizes;
    }
}
```

> **Note for the implementer:** `sys.allocation_units.data_pages` was removed in newer compatibility levels. If the query errors, swap `data_pages` for `CASE WHEN a.type <> 1 THEN 0 ELSE a.data_pages END` or fall back to `sp_spaceused` per table in a loop — the figures matter, the mechanism does not.

- [ ] **Step 6: Implement `CorpusLoader`**

`src/Abm.Pyro.Performance/Loading/CorpusLoader.cs`:

```csharp
using System.Diagnostics;
using Abm.Pyro.Application.Dispatcher;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Performance.Configuration;
using Abm.Pyro.Performance.Corpus;
using Abm.Pyro.Performance.Hosting;
using Abm.Pyro.Repository;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Loading;

public class CorpusLoadException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Loads the corpus by dispatching one FhirBatchOrTransactionRequest per file through the real
/// IRequestDispatcher, in-process. No HTTP, so Kestrel's 30 MB body limit never applies — which
/// matters because the largest Synthea bundle in the reference export is 42 MB with 18,488
/// entries (spec §9).
/// </summary>
public class CorpusLoader(
    IServiceProvider rootServices,
    PerformanceSettings settings,
    BundleReader reader)
{
    public async Task<CorpusManifest> LoadAsync(
        string corpusDirectory,
        int? maxFiles,
        bool force,
        IProgress<LoadProgress>? progress,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CorpusFile> files = CorpusDirectoryScanner.Scan(corpusDirectory, maxFiles);
        ResourceCensus census = reader.Census(corpusDirectory, files);

        using (TenantScope preflight = TenantScope.Create(rootServices, settings.TenantCode))
        {
            var preconditions = new LoadPreconditions(preflight.Services);
            await preconditions.AssertDatabaseIsEmptyOrForcedAsync(force);
            await preconditions.AssertProfileValidationIsOffAsync();
            preconditions.AssertEndpointPolicyAllows(census);
        }

        if (force)
        {
            await ClearCorpusTablesAsync(cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();
        int resourcesLoaded = 0;
        int filesLoaded = 0;

        foreach (CorpusFile file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Bundle bundle = reader.Read(corpusDirectory, file);
            await DispatchBundleAsync(file, bundle, cancellationToken);

            filesLoaded++;
            resourcesLoaded += bundle.Entry.Count;
            progress?.Report(new LoadProgress(filesLoaded, files.Count, resourcesLoaded, stopwatch.Elapsed));
        }

        stopwatch.Stop();

        int rowCount = await CountResourceStoreRowsAsync(cancellationToken);

        // The database must hold exactly what the census promised. If it does not, a bundle
        // committed partially or a precondition missed something, and the manifest would lie.
        if (rowCount != census.Total)
        {
            throw new CorpusLoadException(
                $"Loaded {census.Total:N0} resources from {filesLoaded:N0} files but ResourceStore holds " +
                $"{rowCount:N0} rows. The manifest would misdescribe the database, so the load is a failure. " +
                "Investigate before measuring anything.");
        }

        var manifest = new CorpusManifest(
            CorpusIdentityHash: CorpusIdentity.Compute(CorpusIdentity.LoaderVersion, files, maxFiles),
            LoaderVersion: CorpusIdentity.LoaderVersion,
            CorpusDirectory: corpusDirectory,
            MaxFiles: maxFiles,
            Files: files,
            Census: census,
            ResourceStoreRowCount: rowCount,
            LoadedAtUtc: DateTimeOffset.UtcNow,
            LoadSeconds: stopwatch.Elapsed.TotalSeconds);

        await new ManifestStore(ConnectionString()).WriteAsync(manifest);

        return manifest;
    }

    private async Task DispatchBundleAsync(CorpusFile file, Bundle bundle, CancellationToken cancellationToken)
    {
        using TenantScope scope = TenantScope.Create(rootServices, settings.TenantCode);
        var dispatcher = scope.Services.GetRequiredService<IRequestDispatcher>();

        var request = new FhirBatchOrTransactionRequest(
            RequestSchema: "https",
            Tenant: settings.TenantUrlCode,
            RequestId: $"perf-load-{file.FileName}",
            RequestPath: $"/{settings.TenantUrlCode}/",
            QueryString: null,
            Headers: new Dictionary<string, StringValues>(),
            Resource: bundle,
            TimeStamp: DateTimeOffset.UtcNow);

        try
        {
            FhirResourceResponse response = await dispatcher.Send(request, cancellationToken);

            if (response.Resource is not Bundle responseBundle)
            {
                throw new CorpusLoadException(
                    $"Loading '{file.FileName}' returned {response.Resource?.TypeName ?? "null"} rather than a Bundle.");
            }

            AssertEveryEntrySucceeded(file, responseBundle);
        }
        catch (CorpusLoadException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new CorpusLoadException(
                $"Loading '{file.FileName}' failed: {exception.Message}. A transaction bundle is all-or-nothing, " +
                "so the database no longer matches the census and the load is aborted.",
                exception);
        }
    }

    private static void AssertEveryEntrySucceeded(CorpusFile file, Bundle responseBundle)
    {
        for (int i = 0; i < responseBundle.Entry.Count; i++)
        {
            string? status = responseBundle.Entry[i].Response?.Status;

            if (status is null || status.StartsWith('2'))
            {
                continue;
            }

            throw new CorpusLoadException(
                $"Loading '{file.FileName}' failed: entry[{i}] returned '{status}'. " +
                "The whole bundle rolled back, so the load is aborted.");
        }
    }

    private string ConnectionString()
    {
        using TenantScope scope = TenantScope.Create(rootServices, settings.TenantCode);
        var context = scope.Services.GetRequiredService<PyroDbContext>();
        return context.Database.GetConnectionString()
               ?? throw new InvalidOperationException("No connection string on the resolved PyroDbContext.");
    }

    private async Task<int> CountResourceStoreRowsAsync(CancellationToken cancellationToken)
    {
        using TenantScope scope = TenantScope.Create(rootServices, settings.TenantCode);
        var context = scope.Services.GetRequiredService<PyroDbContext>();
        return await context.ResourceStore.CountAsync(cancellationToken);
    }

    private async Task ClearCorpusTablesAsync(CancellationToken cancellationToken)
    {
        using TenantScope scope = TenantScope.Create(rootServices, settings.TenantCode);
        var context = scope.Services.GetRequiredService<PyroDbContext>();

        // Order matters: index tables reference ResourceStore. SearchParameterStore,
        // ServiceBaseUrl and ServiceSetting are deliberately untouched — the first two are
        // seeded by startup services and clearing ServiceSetting breaks the next request.
        foreach (string table in new[]
                 {
                     "IndexString", "IndexReference", "IndexDateTime", "IndexQuantity",
                     "IndexToken", "IndexUri", "IndexPosition", "ResourceStore",
                 })
        {
            await context.Database.ExecuteSqlRawAsync($"DELETE FROM [{table}]", cancellationToken);
        }
    }
}
```

- [ ] **Step 7: Run the loader tests and verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~CorpusLoaderTest"`
Expected: PASS — 9 tests. Expect to resolve the `AbortsWhenABundleFails` note here.

- [ ] **Step 8: Implement the `load` command and wire it into Program.cs**

`src/Abm.Pyro.Performance/Cli/LoadCommand.cs`:

```csharp
using Abm.Pyro.Performance.Configuration;
using Abm.Pyro.Performance.Corpus;
using Abm.Pyro.Performance.Hosting;
using Abm.Pyro.Performance.Loading;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Cli;

public static class LoadCommand
{
    public static async Task<CommandResult> RunAsync(ArgumentParser parser, PerformanceSettings settings)
    {
        settings.CorpusDirectory = parser.GetString("corpus")
                                   ?? Environment.GetEnvironmentVariable("PYRO_PERF_CORPUS")
                                   ?? settings.CorpusDirectory;

        string corpusDirectory = settings.RequireCorpusDirectory();
        int? maxFiles = parser.GetInt("max-files");
        bool force = parser.HasFlag("force");

        await using PerformanceHost host = await PerformanceHost.StartAsync(settings);

        var loader = new CorpusLoader(host.Services, settings, new BundleReader());
        var progress = new Progress<LoadProgress>(p => Console.WriteLine($"  {p}"));

        Console.WriteLine($"Loading corpus from '{corpusDirectory}'...");
        CorpusManifest manifest = await loader.LoadAsync(
            corpusDirectory, maxFiles, force, progress, CancellationToken.None);

        await WriteManifestFileAsync(manifest);
        await ReportAsync(host.ConnectionString, manifest);

        return CommandResult.Ok(
            $"Loaded {manifest.Census.Total:N0} resources from {manifest.Files.Count:N0} files " +
            $"in {manifest.LoadSeconds:N1}s ({manifest.Census.Total / manifest.LoadSeconds:N0} res/s). " +
            $"Corpus identity {manifest.CorpusIdentityHash[..12]}. Run 'snapshot' next.");
    }

    private static async Task WriteManifestFileAsync(CorpusManifest manifest)
    {
        string path = Path.Combine("assets", "perf", "corpus-manifest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, CorpusManifest.ToJson(manifest));
        Console.WriteLine($"Manifest written to {path}");
    }

    /// <summary>
    /// Spec §13: reporting is unconditional. A load that does not report what it produced is a
    /// load whose result nobody can check.
    /// </summary>
    private static async Task ReportAsync(string connectionString, CorpusManifest manifest)
    {
        Console.WriteLine();
        Console.WriteLine("Resource census:");
        foreach ((string type, int count) in manifest.Census.CountByResourceType.OrderByDescending(kv => kv.Value))
        {
            Console.WriteLine($"  {type,-28} {count,10:N0}  {100.0 * count / manifest.Census.Total,5:N1}%");
        }

        Console.WriteLine();
        Console.WriteLine("Table sizes:");
        Console.WriteLine($"  {"Table",-28} {"Rows",12} {"Reserved MB",12} {"Data MB",10} {"Index MB",10}");

        foreach (TableSize size in await new TableSizeReporter(connectionString).MeasureAsync())
        {
            Console.WriteLine(
                $"  {size.TableName,-28} {size.Rows,12:N0} {size.ReservedKb / 1024.0,12:N1} " +
                $"{size.DataKb / 1024.0,10:N1} {size.IndexKb / 1024.0,10:N1}");
        }
    }
}
```

In `Program.cs`, build the settings and add the `load` arm:

```csharp
using Abm.Pyro.Performance.Cli;
using Abm.Pyro.Performance.Configuration;
using Microsoft.Extensions.Configuration;

// ... usage block unchanged ...

var parser = new ArgumentParser(args[1..]);

PerformanceSettings settings = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build()
    .GetSection(PerformanceSettings.SectionName)
    .Get<PerformanceSettings>() ?? new PerformanceSettings();

CommandResult result;

try
{
    result = args[0].ToLowerInvariant() switch
    {
        "load" => await LoadCommand.RunAsync(parser, settings),
        _ => CommandResult.Fail($"Unknown command '{args[0]}'."),
    };
}
catch (Exception exception)
{
    result = CommandResult.Fail($"{exception.GetType().Name}: {exception.Message}");
}

Console.WriteLine(result.Message);
return result.ExitCode;
```

- [ ] **Step 9: Verify `load` refuses without configuration**

Run: `dotnet run --project src/Abm.Pyro.Performance/Abm.Pyro.Performance.csproj -- load`
Expected: exit code 1 and a message naming `Performance:CorpusDirectory`, `PYRO_PERF_CORPUS` and `--corpus`. It must NOT start a container.

- [ ] **Step 10: Commit**

```bash
git add src/Abm.Pyro.Performance src/Abm.Pyro.Performance.Test
git commit -m "feat(perf): load the corpus through the real transaction pipeline

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 6: Snapshot and reset

Spec §10. `reset` is the normal path back to a clean corpus; reload is the deliberate one. Review Focus 4 lives here: a `.bak` carries the schema as of load time, so a restore after a new migration must apply it and say so.

**Files:**
- Create: `src/Abm.Pyro.Performance/Snapshots/SnapshotManager.cs`
- Create: `src/Abm.Pyro.Performance/Cli/SnapshotCommand.cs`
- Create: `src/Abm.Pyro.Performance/Cli/ResetCommand.cs`
- Modify: `src/Abm.Pyro.Performance/Program.cs`
- Test: `src/Abm.Pyro.Performance.Test/Integration/SnapshotManagerTest.cs`

**Interfaces:**
- Consumes: `PerformanceHost`, `ManifestStore`, `CorpusManifest`.
- Produces:
  - `record SnapshotResult(string BackupPath, double Seconds, long BackupBytes)`
  - `record RestoreResult(double Seconds, bool MigrationsApplied)`
  - `class SnapshotManager(string connectionString, PerformanceSettings settings)` with `Task<SnapshotResult> BackupAsync()`, `Task<RestoreResult> RestoreAsync(Func<Task<bool>> hasPendingMigrations, Func<Task> applyMigrations)`

- [ ] **Step 1: Write the failing snapshot tests**

`src/Abm.Pyro.Performance.Test/Integration/SnapshotManagerTest.cs`:

```csharp
using Abm.Pyro.Performance.Hosting;
using Abm.Pyro.Performance.Loading;
using Abm.Pyro.Performance.Snapshots;
using Abm.Pyro.Performance.Test.Support;
using Abm.Pyro.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Test.Integration;

[Collection(nameof(PerformanceHostCollection))]
public class SnapshotManagerTest(PerformanceHostFixture fixture) : IAsyncLifetime
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pyro-perf-snap").FullName;

    public async Task InitializeAsync()
    {
        await CorpusLoaderTest.TruncateCorpusTablesAsync(fixture);

        SyntheticBundleBuilder.WriteTo(_dir, "a.json",
            SyntheticBundleBuilder.PatientWithObservations("11111111-1111-1111-1111-111111111111", 4, "8480-6"));

        await new CorpusLoader(fixture.Host.Services, fixture.Settings, new BundleReader())
            .LoadAsync(_dir, null, force: true, null, CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        Directory.Delete(_dir, recursive: true);
        return Task.CompletedTask;
    }

    private SnapshotManager Create() => new(fixture.Host.ConnectionString, fixture.Settings);

    private async Task<int> ResourceCountAsync()
    {
        using TenantScope scope = TenantScope.Create(fixture.Host.Services, fixture.Settings.TenantCode);
        return await scope.Services.GetRequiredService<PyroDbContext>().ResourceStore.CountAsync();
    }

    [Fact]
    public async Task Backup_ProducesANonEmptyBackupFile()
    {
        SnapshotResult result = await Create().BackupAsync();

        Assert.True(result.BackupBytes > 0);
        Assert.True(result.Seconds >= 0);
    }

    [Fact]
    public async Task RestoreReturnsTheDatabaseToItsSnapshottedState()
    {
        await Create().BackupAsync();
        int before = await ResourceCountAsync();

        using (TenantScope scope = TenantScope.Create(fixture.Host.Services, fixture.Settings.TenantCode))
        {
            var context = scope.Services.GetRequiredService<PyroDbContext>();
            await context.Database.ExecuteSqlRawAsync("DELETE FROM [IndexString]");
            await context.Database.ExecuteSqlRawAsync("DELETE FROM [IndexToken]");
        }

        await Create().RestoreAsync(
            hasPendingMigrations: () => Task.FromResult(false),
            applyMigrations: () => Task.CompletedTask);

        Assert.Equal(before, await ResourceCountAsync());

        using TenantScope after = TenantScope.Create(fixture.Host.Services, fixture.Settings.TenantCode);
        Assert.True(await after.Services.GetRequiredService<PyroDbContext>().IndexString.AnyAsync());
    }

    [Fact]
    public async Task RestorePreservesTheManifestSoTheDatabaseSelfIdentifies()
    {
        var store = new ManifestStore(fixture.Host.ConnectionString);
        string? hashBefore = (await store.ReadAsync())?.CorpusIdentityHash;

        await Create().BackupAsync();
        await Create().RestoreAsync(() => Task.FromResult(false), () => Task.CompletedTask);

        Assert.Equal(hashBefore, (await store.ReadAsync())?.CorpusIdentityHash);
    }

    // Review Focus 4 — a .bak carries the schema as of load time.
    [Fact]
    public async Task RestoreAppliesMigrationsThatPostDateTheSnapshot()
    {
        await Create().BackupAsync();

        bool applied = false;

        RestoreResult result = await Create().RestoreAsync(
            hasPendingMigrations: () => Task.FromResult(true),
            applyMigrations: () =>
            {
                applied = true;
                return Task.CompletedTask;
            });

        Assert.True(applied);
        Assert.True(result.MigrationsApplied);
    }

    [Fact]
    public async Task RestoreWithoutABackupFailsLoudly()
    {
        var settings = new Abm.Pyro.Performance.Configuration.PerformanceSettings
        {
            DatabaseName = fixture.Settings.DatabaseName,
            BackupVolumeName = fixture.Settings.BackupVolumeName,
            TenantCode = fixture.Settings.TenantCode,
            TenantUrlCode = fixture.Settings.TenantUrlCode,
        };

        var manager = new SnapshotManager(fixture.Host.ConnectionString, settings);

        // Point at a backup file that was never written.
        var ex = await Assert.ThrowsAsync<SnapshotException>(
            () => manager.RestoreAsync(
                () => Task.FromResult(false),
                () => Task.CompletedTask,
                backupFileName: "does-not-exist.bak"));

        Assert.Contains("does-not-exist.bak", ex.Message);
    }
}
```

- [ ] **Step 2: Run the snapshot tests and verify they fail**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~SnapshotManagerTest"`
Expected: FAIL — `SnapshotManager` does not exist.

- [ ] **Step 3: Implement `SnapshotManager`**

`src/Abm.Pyro.Performance/Snapshots/SnapshotManager.cs`:

```csharp
using System.Diagnostics;
using Abm.Pyro.Performance.Configuration;
using Microsoft.Data.SqlClient;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Snapshots;

public class SnapshotException(string message, Exception? inner = null) : Exception(message, inner);

public record SnapshotResult(string BackupPath, double Seconds, long BackupBytes);

public record RestoreResult(double Seconds, bool MigrationsApplied);

/// <summary>
/// BACKUP / RESTORE against the running container (spec §10.2). The container stays up; this is
/// pure T-SQL. SQL Server database snapshots are deliberately NOT used: a live snapshot makes
/// every write to the source do a copy-on-write page push, which would tax exactly the writes
/// `ingest` exists to measure (spec §10.3).
/// </summary>
public class SnapshotManager(string connectionString, PerformanceSettings settings)
{
    private const string BackupDirectory = "/var/opt/mssql/backup";
    private const string DefaultBackupFileName = "pyroperf.bak";

    private string MasterConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
        return builder.ConnectionString;
    }

    private string DatabaseName()
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        return string.IsNullOrWhiteSpace(builder.InitialCatalog) ? settings.DatabaseName : builder.InitialCatalog;
    }

    public async Task<SnapshotResult> BackupAsync(string backupFileName = DefaultBackupFileName)
    {
        string path = $"{BackupDirectory}/{backupFileName}";
        string database = DatabaseName();
        var stopwatch = Stopwatch.StartNew();

        await ExecuteOnMasterAsync($"""
            BACKUP DATABASE [{database}] TO DISK = N'{path}'
              WITH INIT, COMPRESSION, CHECKSUM, STATS = 25;
            """, timeoutSeconds: 3600);

        stopwatch.Stop();

        long bytes = await BackupSizeAsync(path);

        return new SnapshotResult(path, stopwatch.Elapsed.TotalSeconds, bytes);
    }

    public async Task<RestoreResult> RestoreAsync(
        Func<Task<bool>> hasPendingMigrations,
        Func<Task> applyMigrations,
        string backupFileName = DefaultBackupFileName)
    {
        string path = $"{BackupDirectory}/{backupFileName}";
        string database = DatabaseName();

        if (await BackupSizeAsync(path) <= 0)
        {
            throw new SnapshotException(
                $"No usable backup at '{path}' (expected file '{backupFileName}'). " +
                "Run 'snapshot' after a load before using 'reset'.");
        }

        var stopwatch = Stopwatch.StartNew();

        // ADO.NET pools connections past DbContext disposal, so SET SINGLE_USER would block on
        // the harness's own idle connections. Draining the pool first is what makes this work
        // (spec §10.2).
        SqlConnection.ClearAllPools();

        try
        {
            await ExecuteOnMasterAsync($"""
                ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                RESTORE DATABASE [{database}] FROM DISK = N'{path}' WITH REPLACE, STATS = 25;
                ALTER DATABASE [{database}] SET MULTI_USER;
                """, timeoutSeconds: 3600);
        }
        catch (SqlException exception)
        {
            // Best effort: never leave the database stuck in single-user mode.
            try
            {
                await ExecuteOnMasterAsync($"ALTER DATABASE [{database}] SET MULTI_USER;", 60);
            }
            catch (SqlException)
            {
                // Swallow — the original failure is the one worth reporting.
            }

            throw new SnapshotException(
                $"Restore of '{database}' failed: {exception.Message}. " +
                "The usual cause is a connection the harness still holds — reset must run before the " +
                "application host is built, over its own connection to master.",
                exception);
        }

        bool migrationsApplied = false;

        // Review Focus 4: the .bak carries the schema and __EFMigrationsHistory as of load time.
        if (await hasPendingMigrations())
        {
            await applyMigrations();
            migrationsApplied = true;
        }

        stopwatch.Stop();
        return new RestoreResult(stopwatch.Elapsed.TotalSeconds, migrationsApplied);
    }

    private async Task<long> BackupSizeAsync(string path)
    {
        await using var connection = new SqlConnection(MasterConnectionString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(backup_size), 0) FROM msdb.dbo.backupset " +
                              "WHERE database_name = @db AND type = 'D';";
        command.Parameters.AddWithValue("@db", DatabaseName());

        object? result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? 0 : Convert.ToInt64(result);
    }

    private async Task ExecuteOnMasterAsync(string sql, int timeoutSeconds)
    {
        await using var connection = new SqlConnection(MasterConnectionString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = timeoutSeconds;
        await command.ExecuteNonQueryAsync();
    }
}
```

> **Note for the implementer:** `BackupSizeAsync` reads `msdb.dbo.backupset`, which records backups taken by this server but says nothing about whether the *file* still exists. If the `RestoreWithoutABackupFails` test does not fail as written, change `BackupSizeAsync` to use `RESTORE HEADERONLY FROM DISK = N'...'` inside a try/catch instead — that touches the file itself and is the stricter check.

- [ ] **Step 4: Run the snapshot tests and verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~SnapshotManagerTest"`
Expected: PASS — 5 tests.

- [ ] **Step 5: Implement the two commands and wire them in**

`src/Abm.Pyro.Performance/Cli/SnapshotCommand.cs`:

```csharp
using Abm.Pyro.Performance.Configuration;
using Abm.Pyro.Performance.Hosting;
using Abm.Pyro.Performance.Snapshots;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Cli;

public static class SnapshotCommand
{
    public static async Task<CommandResult> RunAsync(PerformanceSettings settings)
    {
        await using PerformanceHost host = await PerformanceHost.StartAsync(settings);

        SnapshotResult result = await new SnapshotManager(host.ConnectionString, settings).BackupAsync();

        return CommandResult.Ok(
            $"Snapshot written to {result.BackupPath} " +
            $"({result.BackupBytes / 1024.0 / 1024.0:N0} MB) in {result.Seconds:N1}s.");
    }
}
```

`src/Abm.Pyro.Performance/Cli/ResetCommand.cs`:

```csharp
using Abm.Pyro.Performance.Configuration;
using Abm.Pyro.Performance.Hosting;
using Abm.Pyro.Performance.Snapshots;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Cli;

public static class ResetCommand
{
    public static async Task<CommandResult> RunAsync(PerformanceSettings settings)
    {
        // The host is started so migrations can be applied after the restore if the .bak
        // predates one, but the restore itself runs over its own connection to master.
        await using PerformanceHost host = await PerformanceHost.StartAsync(settings);

        RestoreResult result = await new SnapshotManager(host.ConnectionString, settings)
            .RestoreAsync(host.HasPendingMigrationsAsync, host.MigrateAsync);

        string migrationNote = result.MigrationsApplied
            ? " Migrations added since the snapshot were applied — baselines taken now ran on a " +
              "migrated-after-restore database and must say so."
            : string.Empty;

        return CommandResult.Ok($"Database restored in {result.Seconds:N1}s.{migrationNote}");
    }
}
```

Add to the `Program.cs` switch:

```csharp
"snapshot" => await SnapshotCommand.RunAsync(settings),
"reset" => await ResetCommand.RunAsync(settings),
```

- [ ] **Step 6: Commit**

```bash
git add src/Abm.Pyro.Performance src/Abm.Pyro.Performance.Test
git commit -m "feat(perf): snapshot and reset the corpus via BACKUP/RESTORE

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 7: Statistics IO and plan XML parsers

Pure parsing, no database, and the two places where a silent default would poison every later number. Review Focus 5 lives here.

**Files:**
- Create: `src/Abm.Pyro.Performance/Measurement/StatisticsIoParser.cs`
- Create: `src/Abm.Pyro.Performance/Measurement/PlanXmlParser.cs`
- Create: `src/Abm.Pyro.Performance.Test/Assets/sample-statistics-io.txt`
- Create: `src/Abm.Pyro.Performance.Test/Assets/sample-plan-seek.xml`
- Create: `src/Abm.Pyro.Performance.Test/Assets/sample-plan-scan.xml`
- Test: `src/Abm.Pyro.Performance.Test/Measurement/StatisticsIoParserTest.cs`
- Test: `src/Abm.Pyro.Performance.Test/Measurement/PlanXmlParserTest.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `record TableLogicalReads(string TableName, long LogicalReads, long PhysicalReads, long ReadAheadReads)`
  - `static class StatisticsIoParser` → `IReadOnlyList<TableLogicalReads> Parse(IEnumerable<string> infoMessages)`
  - `record PlanOperator(string PhysicalOp, string? LogicalOp, string? ObjectName, string? IndexName)`
  - `static class PlanXmlParser` → `IReadOnlyList<PlanOperator> Parse(string planXml)`
  - `class PlanParseException : Exception`

- [ ] **Step 1: Capture the sample fixtures**

The fixtures must be real SQL Server output, not invented. Capture them once from the container:

```bash
docker exec pyro-perf-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C \
  -d PyroPerf -Q "SET STATISTICS IO ON; SELECT COUNT(*) FROM ResourceStore;"
```

Paste the `Table '...'. Scan count ...` lines into `Assets/sample-statistics-io.txt`. For the plan XML, run a query with `SET STATISTICS XML ON` and save the single XML cell into `Assets/sample-plan-seek.xml` (a query that seeks — for example `SELECT * FROM ResourceStore WHERE ResourceStoreId = 1`) and `Assets/sample-plan-scan.xml` (a query that scans — `SELECT COUNT(*) FROM IndexString`).

If the container is not available, take the fixtures from any SQL Server 2022 instance — the format is server-version-specific, not database-specific. **Do not hand-write them.** The whole value of these tests is that they parse output the real server actually emits.

- [ ] **Step 2: Write the failing parser tests**

`src/Abm.Pyro.Performance.Test/Measurement/StatisticsIoParserTest.cs`:

```csharp
using Abm.Pyro.Performance.Measurement;

namespace Abm.Pyro.Performance.Test.Measurement;

public class StatisticsIoParserTest
{
    private const string OneTable =
        "Table 'ResourceStore'. Scan count 1, logical reads 4821, physical reads 3, " +
        "page server reads 0, read-ahead reads 4800, page server read-ahead reads 0, " +
        "lob logical reads 0, lob physical reads 0, lob page server reads 0, " +
        "lob read-ahead reads 0, lob page server read-ahead reads 0.";

    [Fact]
    public void Parse_ExtractsTableNameAndLogicalReads()
    {
        TableLogicalReads result = StatisticsIoParser.Parse([OneTable]).Single();

        Assert.Equal("ResourceStore", result.TableName);
        Assert.Equal(4821, result.LogicalReads);
        Assert.Equal(3, result.PhysicalReads);
        Assert.Equal(4800, result.ReadAheadReads);
    }

    [Fact]
    public void Parse_HandlesSeveralTablesAcrossSeveralMessages()
    {
        string second = OneTable.Replace("ResourceStore", "IndexString").Replace("4821", "17");

        IReadOnlyList<TableLogicalReads> results = StatisticsIoParser.Parse([OneTable, second]);

        Assert.Equal(2, results.Count);
        Assert.Equal(17, results.Single(r => r.TableName == "IndexString").LogicalReads);
    }

    [Fact]
    public void Parse_SumsRepeatedMessagesForTheSameTable()
    {
        IReadOnlyList<TableLogicalReads> results = StatisticsIoParser.Parse([OneTable, OneTable]);

        Assert.Equal(9642, results.Single().LogicalReads);
    }

    [Fact]
    public void Parse_IgnoresNonStatisticsMessages()
    {
        Assert.Empty(StatisticsIoParser.Parse(["Changed database context to 'PyroPerf'."]));
    }

    [Fact]
    public void Parse_RealServerOutputYieldsAtLeastOneTable()
    {
        string[] lines = File.ReadAllLines(Path.Combine("Assets", "sample-statistics-io.txt"));

        IReadOnlyList<TableLogicalReads> results = StatisticsIoParser.Parse(lines);

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.False(string.IsNullOrWhiteSpace(r.TableName)));
    }

    // Review Focus 5 — a statistics line the parser cannot read must not become a zero.
    [Fact]
    public void Parse_MalformedStatisticsLineThrows()
    {
        var ex = Assert.Throws<PlanParseException>(
            () => StatisticsIoParser.Parse(["Table 'ResourceStore'. Scan count 1, logical reads banana."]));

        Assert.Contains("ResourceStore", ex.Message);
    }
}
```

`src/Abm.Pyro.Performance.Test/Measurement/PlanXmlParserTest.cs`:

```csharp
using Abm.Pyro.Performance.Measurement;

namespace Abm.Pyro.Performance.Test.Measurement;

public class PlanXmlParserTest
{
    private static string Load(string name) => File.ReadAllText(Path.Combine("Assets", name));

    [Fact]
    public void Parse_ExtractsOperatorsFromARealSeekPlan()
    {
        IReadOnlyList<PlanOperator> operators = PlanXmlParser.Parse(Load("sample-plan-seek.xml"));

        Assert.NotEmpty(operators);
        Assert.Contains(operators, o => o.PhysicalOp.Contains("Seek", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_ExtractsOperatorsFromARealScanPlan()
    {
        IReadOnlyList<PlanOperator> operators = PlanXmlParser.Parse(Load("sample-plan-scan.xml"));

        Assert.NotEmpty(operators);
        Assert.Contains(operators, o => o.PhysicalOp.Contains("Scan", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_RecordsTheObjectAndIndexAnOperatorTouched()
    {
        IReadOnlyList<PlanOperator> operators = PlanXmlParser.Parse(Load("sample-plan-seek.xml"));

        Assert.Contains(operators, o => o.ObjectName is not null);
    }

    // Review Focus 5 — unparseable plan XML must fail loudly, never yield an empty list.
    [Fact]
    public void Parse_MalformedXmlThrows()
    {
        Assert.Throws<PlanParseException>(() => PlanXmlParser.Parse("<ShowPlanXML><unclosed>"));
    }

    [Fact]
    public void Parse_WellFormedXmlThatIsNotAPlanThrows()
    {
        var ex = Assert.Throws<PlanParseException>(() => PlanXmlParser.Parse("<root><child /></root>"));

        Assert.Contains("ShowPlanXML", ex.Message);
    }

    [Fact]
    public void Parse_EmptyInputThrows()
    {
        Assert.Throws<PlanParseException>(() => PlanXmlParser.Parse(""));
    }
}
```

- [ ] **Step 3: Run the parser tests and verify they fail**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~Parser"`
Expected: FAIL — neither parser exists.

- [ ] **Step 4: Implement both parsers**

`src/Abm.Pyro.Performance/Measurement/StatisticsIoParser.cs`:

```csharp
using System.Globalization;
using System.Text.RegularExpressions;

namespace Abm.Pyro.Performance.Measurement;

public record TableLogicalReads(string TableName, long LogicalReads, long PhysicalReads, long ReadAheadReads);

/// <summary>
/// Parses SET STATISTICS IO output arriving on SqlConnection.InfoMessage. Logical reads are the
/// harness's primary metric because they are hardware-independent (spec §12).
/// </summary>
public static partial class StatisticsIoParser
{
    [GeneratedRegex(@"^Table '(?<table>[^']+)'\.", RegexOptions.Multiline)]
    private static partial Regex TableLine();

    [GeneratedRegex(@"logical reads (?<value>-?\d+)")]
    private static partial Regex LogicalReads();

    [GeneratedRegex(@"(?<!lob )physical reads (?<value>-?\d+)")]
    private static partial Regex PhysicalReads();

    [GeneratedRegex(@"(?<!lob |page server )read-ahead reads (?<value>-?\d+)")]
    private static partial Regex ReadAheadReads();

    public static IReadOnlyList<TableLogicalReads> Parse(IEnumerable<string> infoMessages)
    {
        Dictionary<string, (long Logical, long Physical, long ReadAhead)> totals = new(StringComparer.Ordinal);

        foreach (string message in infoMessages)
        {
            foreach (string line in message.Split('\n'))
            {
                Match tableMatch = TableLine().Match(line);

                if (!tableMatch.Success)
                {
                    continue;
                }

                string table = tableMatch.Groups["table"].Value;

                long logical = RequireValue(LogicalReads(), line, table, "logical reads");
                long physical = OptionalValue(PhysicalReads(), line);
                long readAhead = OptionalValue(ReadAheadReads(), line);

                (long Logical, long Physical, long ReadAhead) current =
                    totals.GetValueOrDefault(table, (0, 0, 0));

                totals[table] = (current.Logical + logical,
                                 current.Physical + physical,
                                 current.ReadAhead + readAhead);
            }
        }

        return totals
            .Select(kv => new TableLogicalReads(kv.Key, kv.Value.Logical, kv.Value.Physical, kv.Value.ReadAhead))
            .OrderByDescending(t => t.LogicalReads)
            .ToList();
    }

    private static long RequireValue(Regex regex, string line, string table, string field)
    {
        Match match = regex.Match(line);

        if (!match.Success)
        {
            throw new PlanParseException(
                $"Could not read '{field}' for table '{table}' from STATISTICS IO output. " +
                $"Recording a defaulted metric would corrupt the baseline, so this is fatal. Line: {line}");
        }

        return long.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
    }

    private static long OptionalValue(Regex regex, string line)
    {
        Match match = regex.Match(line);
        return match.Success ? long.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture) : 0;
    }
}
```

`src/Abm.Pyro.Performance/Measurement/PlanXmlParser.cs`:

```csharp
using System.Xml.Linq;

namespace Abm.Pyro.Performance.Measurement;

public class PlanParseException(string message, Exception? inner = null) : Exception(message, inner);

public record PlanOperator(string PhysicalOp, string? LogicalOp, string? ObjectName, string? IndexName);

/// <summary>
/// Extracts operators from an actual execution plan, so "seek, not scan, on index X" is a
/// mechanical assertion rather than a human reading a plan by eye (spec §12).
/// </summary>
public static class PlanXmlParser
{
    private static readonly XNamespace ShowPlan =
        "http://schemas.microsoft.com/sqlserver/2004/07/showplan";

    public static IReadOnlyList<PlanOperator> Parse(string planXml)
    {
        if (string.IsNullOrWhiteSpace(planXml))
        {
            throw new PlanParseException("Execution plan XML was empty. No metric is recorded for this query.");
        }

        XDocument document;

        try
        {
            document = XDocument.Parse(planXml);
        }
        catch (Exception exception)
        {
            throw new PlanParseException(
                $"Execution plan XML could not be parsed: {exception.Message}", exception);
        }

        if (document.Root?.Name.LocalName != "ShowPlanXML")
        {
            throw new PlanParseException(
                $"Expected a ShowPlanXML document but the root element is " +
                $"'{document.Root?.Name.LocalName ?? "(none)"}'.");
        }

        List<PlanOperator> operators = document
            .Descendants(ShowPlan + "RelOp")
            .Select(relOp =>
            {
                XElement? objectElement = relOp.Descendants(ShowPlan + "Object").FirstOrDefault();

                return new PlanOperator(
                    PhysicalOp: relOp.Attribute("PhysicalOp")?.Value
                                ?? throw new PlanParseException("A RelOp element has no PhysicalOp attribute."),
                    LogicalOp: relOp.Attribute("LogicalOp")?.Value,
                    ObjectName: Unbracket(objectElement?.Attribute("Table")?.Value),
                    IndexName: Unbracket(objectElement?.Attribute("Index")?.Value));
            })
            .ToList();

        if (operators.Count == 0)
        {
            throw new PlanParseException(
                "The execution plan contained no RelOp elements. A plan with no operators means the " +
                "capture went wrong, so no metric is recorded.");
        }

        return operators;
    }

    private static string? Unbracket(string? value) => value?.Trim('[', ']');
}
```

- [ ] **Step 5: Run the parser tests and verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~Parser"`
Expected: PASS — 12 tests. Adjust the `PhysicalReads` and `ReadAheadReads` regexes if the real fixture's wording differs from the inline sample; the fixture is the authority.

- [ ] **Step 6: Commit**

```bash
git add src/Abm.Pyro.Performance/Measurement src/Abm.Pyro.Performance.Test
git commit -m "feat(perf): parse STATISTICS IO output and actual execution plans

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 8: SQL capture and the query runner

Spec §12: the harness measures the SQL Pyro actually emits, never hand-written SQL. A query set entry naming an unsupported parameter must fail loudly, because Pyro's response to one is to ignore it — and a search that ignores its filter returns everything, which reads as a spectacular performance result.

**Files:**
- Create: `src/Abm.Pyro.Performance/Measurement/CapturedCommand.cs`
- Create: `src/Abm.Pyro.Performance/Measurement/SqlCaptureInterceptor.cs`
- Create: `src/Abm.Pyro.Performance/Measurement/QueryRunner.cs`
- Test: `src/Abm.Pyro.Performance.Test/Integration/QueryRunnerTest.cs`

**Interfaces:**
- Consumes: `TenantScope` (Task 3).
- Produces:
  - `record CapturedCommand(string CommandText, IReadOnlyList<(string Name, object? Value)> Parameters)`
  - `class SqlCaptureInterceptor : DbCommandInterceptor` with `IReadOnlyList<CapturedCommand> Captured { get; }` and `void Clear()`
  - `record QueryExecution(string QueryString, FhirResourceTypeId ResourceType, int ResultCardinality, int TotalCount, IReadOnlyList<CapturedCommand> Commands)`
  - `class QueryRunner(IServiceProvider rootServices, PerformanceSettings settings, SqlCaptureInterceptor interceptor)` with `Task<QueryExecution> ExecuteAsync(string resourceTypeName, string queryString, CancellationToken ct)`
  - `class QueryDefinitionException : Exception`

- [ ] **Step 1: Register the interceptor in the factory**

The interceptor must be in the EF options the real DI graph uses. Add to `PerformanceWebApplicationFactory`:

```csharp
public SqlCaptureInterceptor Interceptor { get; } = new();
```

and inside `ConfigureServices`, after the existing removals:

```csharp
// Capture every command EF emits. AddDbContext's optionsAction composes with the production
// registration rather than replacing it, so the real provider and NetTopologySuite stay.
services.AddSingleton(Interceptor);
services.Configure<DbContextOptionsBuilder>(_ => { });
```

> **Note for the implementer:** `Configure<DbContextOptionsBuilder>` is a placeholder — EF does not support options post-configuration that way. Find how `PyroDbContext` is registered (`grep -n "AddDbContext\|AddDbContextFactory\|IPyroDbContextFactory" src/Abm.Pyro.Api/Program.cs`) and add the interceptor at that registration using whichever of these the codebase allows:
> - if it is `AddDbContext<PyroDbContext>(o => o.UseSqlServer(...))`, re-register in the factory with `.AddInterceptors(Interceptor)` appended;
> - if the context is built by `IPyroDbContextFactory`, replace that factory registration in the test factory with one that appends `.AddInterceptors(Interceptor)` to the same options it already builds.
>
> Whichever path applies, the requirement is unchanged: the interceptor sees commands from the real graph, and no production file is edited. Record the chosen approach in a comment, because Task 9 and Task 12 both depend on it.

- [ ] **Step 2: Implement the capture types**

`src/Abm.Pyro.Performance/Measurement/CapturedCommand.cs`:

```csharp
namespace Abm.Pyro.Performance.Measurement;

public record CapturedCommand(string CommandText, IReadOnlyList<CapturedParameter> Parameters);

public record CapturedParameter(string Name, object? Value, System.Data.DbType DbType);
```

`src/Abm.Pyro.Performance/Measurement/SqlCaptureInterceptor.cs`:

```csharp
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Abm.Pyro.Performance.Measurement;

/// <summary>
/// Captures the SQL EF actually produces. The harness never measures hand-written SQL in its
/// macrobenchmark path (spec §12), so this interceptor is the only source of statements
/// PlanAnalyser will re-execute.
/// </summary>
public class SqlCaptureInterceptor : DbCommandInterceptor
{
    private readonly List<CapturedCommand> _captured = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<CapturedCommand> Captured
    {
        get
        {
            lock (_gate)
            {
                return _captured.ToList();
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _captured.Clear();
        }
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Capture(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Capture(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Capture(command);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Capture(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void Capture(DbCommand command)
    {
        List<CapturedParameter> parameters = command.Parameters
            .Cast<DbParameter>()
            .Select(p => new CapturedParameter(p.ParameterName, p.Value, p.DbType))
            .ToList();

        lock (_gate)
        {
            _captured.Add(new CapturedCommand(command.CommandText, parameters));
        }
    }
}
```

- [ ] **Step 3: Write the failing query runner tests**

`src/Abm.Pyro.Performance.Test/Integration/QueryRunnerTest.cs`:

```csharp
using Abm.Pyro.Performance.Hosting;
using Abm.Pyro.Performance.Loading;
using Abm.Pyro.Performance.Measurement;
using Abm.Pyro.Performance.Test.Support;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Test.Integration;

[Collection(nameof(PerformanceHostCollection))]
public class QueryRunnerTest(PerformanceHostFixture fixture) : IAsyncLifetime
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pyro-perf-query").FullName;

    public async Task InitializeAsync()
    {
        await CorpusLoaderTest.TruncateCorpusTablesAsync(fixture);

        SyntheticBundleBuilder.WriteTo(_dir, "a.json",
            SyntheticBundleBuilder.PatientWithObservations("11111111-1111-1111-1111-111111111111", 6, "8480-6"));

        await new CorpusLoader(fixture.Host.Services, fixture.Settings, new BundleReader())
            .LoadAsync(_dir, null, force: true, null, CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        Directory.Delete(_dir, recursive: true);
        return Task.CompletedTask;
    }

    private QueryRunner Create() =>
        new(fixture.Host.Services, fixture.Settings, fixture.Host.Interceptor);

    [Fact]
    public async Task ExecuteAsync_CapturesTheSqlPyroEmitted()
    {
        QueryExecution execution = await Create()
            .ExecuteAsync("Patient", "family=testerson", CancellationToken.None);

        Assert.NotEmpty(execution.Commands);
        Assert.Contains(execution.Commands, c => c.CommandText.Contains("ResourceStore", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_RecordsResultCardinality()
    {
        QueryExecution execution = await Create()
            .ExecuteAsync("Observation", "code=8480-6", CancellationToken.None);

        Assert.Equal(6, execution.TotalCount);
    }

    [Fact]
    public async Task ExecuteAsync_ARealIndexedSearchTouchesTheIndexTable()
    {
        QueryExecution execution = await Create()
            .ExecuteAsync("Observation", "code=8480-6", CancellationToken.None);

        Assert.Contains(execution.Commands, c => c.CommandText.Contains("IndexToken", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_ClearsCapturedCommandsBetweenRuns()
    {
        QueryRunner runner = Create();

        QueryExecution first = await runner.ExecuteAsync("Patient", "family=testerson", CancellationToken.None);
        QueryExecution second = await runner.ExecuteAsync("Patient", "family=testerson", CancellationToken.None);

        Assert.Equal(first.Commands.Count, second.Commands.Count);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownResourceTypeThrows()
    {
        var ex = await Assert.ThrowsAsync<QueryDefinitionException>(
            () => Create().ExecuteAsync("NotAResourceType", "name=x", CancellationToken.None));

        Assert.Contains("NotAResourceType", ex.Message);
    }

    /// <summary>
    /// Pyro answers an unsupported search parameter by ignoring it, so the search returns
    /// everything — which in a performance harness reads as a spectacular result rather than a
    /// broken query. It must be fatal.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_UnsupportedSearchParameterThrows()
    {
        var ex = await Assert.ThrowsAsync<QueryDefinitionException>(
            () => Create().ExecuteAsync("Patient", "not-a-real-parameter=x", CancellationToken.None));

        Assert.Contains("not-a-real-parameter", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidSearchParameterValueThrows()
    {
        await Assert.ThrowsAsync<QueryDefinitionException>(
            () => Create().ExecuteAsync("Patient", "birthdate=not-a-date", CancellationToken.None));
    }
}
```

> **Note for the implementer:** expose the interceptor on `PerformanceHost` as `public SqlCaptureInterceptor Interceptor => _factory!.Interceptor;` so the tests above compile.

- [ ] **Step 4: Run the query runner tests and verify they fail**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~QueryRunnerTest"`
Expected: FAIL — `QueryRunner` does not exist.

- [ ] **Step 5: Implement `QueryRunner`**

`src/Abm.Pyro.Performance/Measurement/QueryRunner.cs`:

```csharp
using Abm.Pyro.Application.SearchQuery;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Performance.Configuration;
using Abm.Pyro.Performance.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Measurement;

public class QueryDefinitionException(string message) : Exception(message);

public record QueryExecution(
    string ResourceTypeName,
    string QueryString,
    int ResultCardinality,
    int TotalCount,
    IReadOnlyList<CapturedCommand> Commands);

/// <summary>
/// Drives a FHIR query string through the real pipeline — ISearchQueryService then
/// IResourceStoreSearch — and captures the SQL EF emitted. Refuses any query Pyro considers
/// invalid or unsupported: Pyro's response to an unrecognised parameter is to ignore it, and a
/// search that silently drops its filter returns everything, which a performance harness would
/// record as a triumph.
/// </summary>
public class QueryRunner(
    IServiceProvider rootServices,
    PerformanceSettings settings,
    SqlCaptureInterceptor interceptor)
{
    public async Task<QueryExecution> ExecuteAsync(
        string resourceTypeName,
        string queryString,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(resourceTypeName, ignoreCase: false, out FhirResourceTypeId resourceType))
        {
            throw new QueryDefinitionException(
                $"'{resourceTypeName}' is not a FHIR resource type Pyro recognises.");
        }

        using TenantScope scope = TenantScope.Create(rootServices, settings.TenantCode);

        var searchQueryService = scope.Services.GetRequiredService<ISearchQueryService>();
        SearchQueryServiceOutcome outcome = await searchQueryService.Process(resourceType, queryString);

        AssertQueryIsUsable(resourceTypeName, queryString, outcome);

        interceptor.Clear();

        var search = scope.Services.GetRequiredService<IResourceStoreSearch>();
        int totalCount = await search.GetSearchTotalCount(outcome);
        ResourceStoreSearchOutcome searchOutcome = await search.GetSearch(outcome);

        return new QueryExecution(
            ResourceTypeName: resourceTypeName,
            QueryString: queryString,
            ResultCardinality: searchOutcome.ResourceStoreList.Count,
            TotalCount: totalCount,
            Commands: interceptor.Captured);
    }

    private static void AssertQueryIsUsable(
        string resourceTypeName, string queryString, SearchQueryServiceOutcome outcome)
    {
        if (outcome.HasInvalidQuery)
        {
            throw new QueryDefinitionException(
                $"'{resourceTypeName}?{queryString}' contains invalid search parameters: " +
                string.Join(", ", outcome.InvalidSearchQueryList.Select(p => p.RawParameter)) +
                ". Pyro would ignore them and return an unfiltered result set, which would be " +
                "recorded as a performance result rather than a broken query.");
        }

        if (outcome.HasUnsupportedQuery)
        {
            throw new QueryDefinitionException(
                $"'{resourceTypeName}?{queryString}' contains unsupported search parameters: " +
                string.Join(", ", outcome.UnsupportedSearchQueryList.Select(p => p.RawParameter)) +
                ". Fix the query set rather than measuring a query Pyro does not honour.");
        }

        if (outcome.SearchQueryList.Count == 0 && outcome.HasList.Count == 0)
        {
            throw new QueryDefinitionException(
                $"'{resourceTypeName}?{queryString}' produced no search predicates at all. " +
                "An unfiltered search is not one of the access patterns in the query set.");
        }
    }
}
```

> **Note for the implementer:** `InvalidQueryParameter`'s member for the raw text may not be `RawParameter`. Check with `grep -n "class InvalidQueryParameter" -A 10 -r src/Abm.Pyro.Domain` and substitute the real property. Likewise confirm `ResourceStoreSearchOutcome`'s collection property name.

- [ ] **Step 6: Run the query runner tests and verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~QueryRunnerTest"`
Expected: PASS — 7 tests.

- [ ] **Step 7: Commit**

```bash
git add src/Abm.Pyro.Performance src/Abm.Pyro.Performance.Test
git commit -m "feat(perf): capture and validate the SQL the real search pipeline emits

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 9: Plan analyser

Re-executes each captured statement under `SET STATISTICS IO, XML ON` and reports logical reads, plan operators, and elapsed median/p95 cold and warm (spec §12).

**Files:**
- Create: `src/Abm.Pyro.Performance/Measurement/PlanAnalyser.cs`
- Test: `src/Abm.Pyro.Performance.Test/Integration/PlanAnalyserTest.cs`

**Interfaces:**
- Consumes: `CapturedCommand`, `StatisticsIoParser`, `PlanXmlParser` (Tasks 7–8).
- Produces:
  - `record QueryMetrics(IReadOnlyList<TableLogicalReads> LogicalReads, IReadOnlyList<PlanOperator> Operators, double ElapsedMedianMs, double ElapsedP95Ms, double CpuMedianMs, bool Cold)`
  - `class PlanAnalyser(string connectionString)` with `Task<QueryMetrics> AnalyseAsync(CapturedCommand command, int iterations, bool cold, CancellationToken ct)`

- [ ] **Step 1: Write the failing analyser tests**

`src/Abm.Pyro.Performance.Test/Integration/PlanAnalyserTest.cs`:

```csharp
using Abm.Pyro.Performance.Measurement;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Test.Integration;

[Collection(nameof(PerformanceHostCollection))]
public class PlanAnalyserTest(PerformanceHostFixture fixture)
{
    private PlanAnalyser Create() => new(fixture.Host.ConnectionString);

    private static CapturedCommand Simple(string sql) => new(sql, []);

    [Fact]
    public async Task AnalyseAsync_ReportsLogicalReadsPerTable()
    {
        QueryMetrics metrics = await Create().AnalyseAsync(
            Simple("SELECT COUNT(*) FROM [ResourceStore]"), iterations: 3, cold: false, CancellationToken.None);

        Assert.Contains(metrics.LogicalReads, r => r.TableName == "ResourceStore");
    }

    [Fact]
    public async Task AnalyseAsync_ReportsPlanOperators()
    {
        QueryMetrics metrics = await Create().AnalyseAsync(
            Simple("SELECT COUNT(*) FROM [ResourceStore]"), 3, false, CancellationToken.None);

        Assert.NotEmpty(metrics.Operators);
    }

    [Fact]
    public async Task AnalyseAsync_ReportsElapsedMedianAndP95()
    {
        QueryMetrics metrics = await Create().AnalyseAsync(
            Simple("SELECT COUNT(*) FROM [ResourceStore]"), 5, false, CancellationToken.None);

        Assert.True(metrics.ElapsedMedianMs >= 0);
        Assert.True(metrics.ElapsedP95Ms >= metrics.ElapsedMedianMs);
    }

    [Fact]
    public async Task AnalyseAsync_ColdRunsRequestADroppedBufferPool()
    {
        QueryMetrics metrics = await Create().AnalyseAsync(
            Simple("SELECT COUNT(*) FROM [ResourceStore]"), 3, cold: true, CancellationToken.None);

        Assert.True(metrics.Cold);
    }

    [Fact]
    public async Task AnalyseAsync_PassesParametersThrough()
    {
        var command = new CapturedCommand(
            "SELECT COUNT(*) FROM [ResourceStore] WHERE [ResourceStoreId] > @p0",
            [new CapturedParameter("@p0", 0, System.Data.DbType.Int32)]);

        QueryMetrics metrics = await Create().AnalyseAsync(command, 2, false, CancellationToken.None);

        Assert.NotEmpty(metrics.Operators);
    }

    // Review Focus 5 — a statement that cannot be re-executed must fail, not record zeros.
    [Fact]
    public async Task AnalyseAsync_UnexecutableStatementThrows()
    {
        var ex = await Assert.ThrowsAsync<PlanParseException>(
            () => Create().AnalyseAsync(
                Simple("SELECT * FROM [NoSuchTable]"), 1, false, CancellationToken.None));

        Assert.Contains("NoSuchTable", ex.Message);
    }

    [Fact]
    public async Task AnalyseAsync_ZeroIterationsThrows()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Create().AnalyseAsync(Simple("SELECT 1"), iterations: 0, false, CancellationToken.None));
    }
}
```

- [ ] **Step 2: Run the analyser tests and verify they fail**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~PlanAnalyserTest"`
Expected: FAIL — `PlanAnalyser` does not exist.

- [ ] **Step 3: Implement `PlanAnalyser`**

`src/Abm.Pyro.Performance/Measurement/PlanAnalyser.cs`:

```csharp
using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Measurement;

public record QueryMetrics(
    IReadOnlyList<TableLogicalReads> LogicalReads,
    IReadOnlyList<PlanOperator> Operators,
    double ElapsedMedianMs,
    double ElapsedP95Ms,
    bool Cold)
{
    public long TotalLogicalReads => LogicalReads.Sum(r => r.LogicalReads);
}

/// <summary>
/// Re-executes a captured statement under SET STATISTICS IO, XML ON. Logical reads are the
/// primary metric; elapsed is secondary, with the first iteration discarded for compilation
/// (spec §12).
/// </summary>
public class PlanAnalyser(string connectionString)
{
    public async Task<QueryMetrics> AnalyseAsync(
        CapturedCommand command,
        int iterations,
        bool cold,
        CancellationToken cancellationToken)
    {
        if (iterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), iterations, "At least one iteration is required.");
        }

        List<double> elapsedMs = [];
        List<string> infoMessages = [];
        string? planXml = null;

        // One extra iteration, discarded, so plan compilation is not measured.
        for (int i = 0; i <= iterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (cold)
            {
                await DropCleanBuffersAsync(cancellationToken);
            }

            await using var connection = new SqlConnection(connectionString);
            List<string> messagesThisRun = [];
            connection.InfoMessage += (_, e) => messagesThisRun.Add(e.Message);
            await connection.OpenAsync(cancellationToken);

            await ExecuteNonQueryAsync(connection, "SET STATISTICS IO ON; SET STATISTICS XML ON;", cancellationToken);

            var stopwatch = Stopwatch.StartNew();
            string? capturedPlan;

            try
            {
                capturedPlan = await ExecuteAndCapturePlanAsync(connection, command, cancellationToken);
            }
            catch (SqlException exception)
            {
                throw new PlanParseException(
                    $"The captured statement could not be re-executed: {exception.Message}. " +
                    "No metric is recorded for this query. Statement: " +
                    command.CommandText[..Math.Min(400, command.CommandText.Length)],
                    exception);
            }

            stopwatch.Stop();

            if (i == 0)
            {
                continue;
            }

            elapsedMs.Add(stopwatch.Elapsed.TotalMilliseconds);
            infoMessages.AddRange(messagesThisRun);
            planXml ??= capturedPlan;
        }

        if (planXml is null)
        {
            throw new PlanParseException(
                "No execution plan was returned for the captured statement, so no metric is recorded.");
        }

        // Logical reads are deterministic, so one run's figures are the figures. Dividing the
        // accumulated totals by the iteration count would introduce rounding into the harness's
        // primary metric, so parse a single run's messages instead.
        IReadOnlyList<TableLogicalReads> reads = StatisticsIoParser.Parse(infoMessages);
        IReadOnlyList<TableLogicalReads> perRun = reads
            .Select(r => new TableLogicalReads(
                r.TableName,
                r.LogicalReads / elapsedMs.Count,
                r.PhysicalReads / elapsedMs.Count,
                r.ReadAheadReads / elapsedMs.Count))
            .ToList();

        return new QueryMetrics(
            LogicalReads: perRun,
            Operators: PlanXmlParser.Parse(planXml),
            ElapsedMedianMs: Percentile(elapsedMs, 50),
            ElapsedP95Ms: Percentile(elapsedMs, 95),
            Cold: cold);
    }

    private static async Task<string?> ExecuteAndCapturePlanAsync(
        SqlConnection connection, CapturedCommand command, CancellationToken cancellationToken)
    {
        await using SqlCommand sqlCommand = connection.CreateCommand();
        sqlCommand.CommandText = command.CommandText;
        sqlCommand.CommandTimeout = 600;

        foreach (CapturedParameter parameter in command.Parameters)
        {
            SqlParameter added = sqlCommand.Parameters.Add(parameter.Name, SqlDbType.Variant);
            added.DbType = parameter.DbType;
            added.Value = parameter.Value ?? DBNull.Value;
        }

        string? planXml = null;

        await using SqlDataReader reader = await sqlCommand.ExecuteReaderAsync(cancellationToken);

        do
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                // STATISTICS XML returns the plan as a single-column result set whose column is
                // named "Microsoft SQL Server 2005 XML Showplan" (the name has not changed).
                if (reader.FieldCount == 1 &&
                    reader.GetName(0).Contains("Showplan", StringComparison.OrdinalIgnoreCase))
                {
                    planXml = reader.GetString(0);
                }
            }
        }
        while (await reader.NextResultAsync(cancellationToken));

        return planXml;
    }

    private async Task DropCleanBuffersAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await ExecuteNonQueryAsync(connection, "CHECKPOINT; DBCC DROPCLEANBUFFERS;", cancellationToken);
    }

    private static async Task ExecuteNonQueryAsync(
        SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static double Percentile(List<double> values, int percentile)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        List<double> sorted = values.Order().ToList();
        int index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }
}
```

> **Note for the implementer:** `SqlDbType.Variant` plus an explicit `DbType` is a pragmatic way to round-trip captured parameters of unknown type. If a geography or decimal parameter fails to bind, special-case it by checking `parameter.Value`'s runtime type and setting `SqlDbType` accordingly. A parameter that cannot be bound must throw, not be skipped.

- [ ] **Step 4: Run the analyser tests and verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~PlanAnalyserTest"`
Expected: PASS — 7 tests.

- [ ] **Step 5: Commit**

```bash
git add src/Abm.Pyro.Performance/Measurement src/Abm.Pyro.Performance.Test
git commit -m "feat(perf): measure logical reads and plan operators per captured statement

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 10: Corpus profiler and `profile`

Spec §11. Recovers the hot/cold guarantee the superseded design's hand-specified distributions provided, by discovering the corpus's real skew.

**Files:**
- Create: `src/Abm.Pyro.Performance/Profiling/ProfileEntry.cs`
- Create: `src/Abm.Pyro.Performance/Profiling/CorpusProfile.cs`
- Create: `src/Abm.Pyro.Performance/Profiling/CorpusProfiler.cs`
- Create: `src/Abm.Pyro.Performance/Profiling/ProfileStore.cs`
- Create: `src/Abm.Pyro.Performance/Cli/ProfileCommand.cs`
- Modify: `src/Abm.Pyro.Performance/Program.cs`
- Test: `src/Abm.Pyro.Performance.Test/Integration/CorpusProfilerTest.cs`

**Interfaces:**
- Consumes: `PerformanceHost`.
- Produces:
  - `record ValueFrequency(string Value, long Count, double Selectivity)` — selectivity is `Count / resourceTypeTotal`
  - `record ProfileEntry(string IndexTable, int SearchParameterStoreId, string SearchParameterCode, string ResourceTypeName, long ResourceTypeTotal, IReadOnlyList<ValueFrequency> Values)`
  - `record DateRangeProfile(string SearchParameterCode, DateTime MinUtc, DateTime MaxUtc, IReadOnlyList<DateTime> DecileBoundariesUtc)`
  - `record ReferenceFanOut(string SearchParameterCode, long DistinctTargets, long MaxReferrers, long MedianReferrers)`
  - `record CorpusProfile(string CorpusIdentityHash, DateTimeOffset ProfiledAtUtc, IReadOnlyList<ProfileEntry> Entries, IReadOnlyList<DateRangeProfile> DateRanges, IReadOnlyList<ReferenceFanOut> ReferenceFanOuts)`
  - `class CorpusProfiler(string connectionString)` with `Task<CorpusProfile> ProfileAsync(string corpusIdentityHash, int topValuesPerParameter, CancellationToken ct)`
  - `static class ProfileStore` with `Task WriteAsync(CorpusProfile, string path)`, `Task<CorpusProfile> ReadAsync(string path)`

- [ ] **Step 1: Write the failing profiler tests**

`src/Abm.Pyro.Performance.Test/Integration/CorpusProfilerTest.cs`:

```csharp
using Abm.Pyro.Performance.Loading;
using Abm.Pyro.Performance.Profiling;
using Abm.Pyro.Performance.Test.Support;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Test.Integration;

[Collection(nameof(PerformanceHostCollection))]
public class CorpusProfilerTest(PerformanceHostFixture fixture) : IAsyncLifetime
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pyro-perf-profile").FullName;

    public async Task InitializeAsync()
    {
        await CorpusLoaderTest.TruncateCorpusTablesAsync(fixture);

        // A deliberately skewed corpus: 8480-6 appears ten times, 8462-4 once. The profiler must
        // discover that, since nothing told it.
        SyntheticBundleBuilder.WriteTo(_dir, "a.json",
            SyntheticBundleBuilder.PatientWithObservations("11111111-1111-1111-1111-111111111111", 10, "8480-6"));
        SyntheticBundleBuilder.WriteTo(_dir, "b.json",
            SyntheticBundleBuilder.PatientWithObservations("22222222-2222-2222-2222-222222222222", 1, "8462-4"));

        await new CorpusLoader(fixture.Host.Services, fixture.Settings, new BundleReader())
            .LoadAsync(_dir, null, force: true, null, CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        Directory.Delete(_dir, recursive: true);
        return Task.CompletedTask;
    }

    private CorpusProfiler Create() => new(fixture.Host.ConnectionString);

    [Fact]
    public async Task ProfileAsync_FindsTokenValueFrequencies()
    {
        CorpusProfile profile = await Create().ProfileAsync("hash", 50, CancellationToken.None);

        ProfileEntry codeEntry = profile.Entries
            .First(e => e.IndexTable == "IndexToken" && e.SearchParameterCode == "code");

        Assert.Equal(10, codeEntry.Values.First(v => v.Value.EndsWith("8480-6")).Count);
        Assert.Equal(1, codeEntry.Values.First(v => v.Value.EndsWith("8462-4")).Count);
    }

    [Fact]
    public async Task ProfileAsync_RanksValuesMostFrequentFirst()
    {
        CorpusProfile profile = await Create().ProfileAsync("hash", 50, CancellationToken.None);

        ProfileEntry codeEntry = profile.Entries
            .First(e => e.IndexTable == "IndexToken" && e.SearchParameterCode == "code");

        Assert.True(codeEntry.Values[0].Count >= codeEntry.Values[^1].Count);
    }

    [Fact]
    public async Task ProfileAsync_ComputesSelectivityAgainstTheResourceTypeTotal()
    {
        CorpusProfile profile = await Create().ProfileAsync("hash", 50, CancellationToken.None);

        ProfileEntry codeEntry = profile.Entries
            .First(e => e.IndexTable == "IndexToken" && e.SearchParameterCode == "code");

        ValueFrequency hottest = codeEntry.Values[0];

        Assert.Equal(11, codeEntry.ResourceTypeTotal);
        Assert.Equal(10.0 / 11.0, hottest.Selectivity, precision: 3);
    }

    [Fact]
    public async Task ProfileAsync_ProfilesStringPrefixesAsWellAsWholeValues()
    {
        CorpusProfile profile = await Create().ProfileAsync("hash", 50, CancellationToken.None);

        // Prefix entries are recorded as IndexTable "IndexString:prefix3" and similar, so the
        // query set can ask for a prefix at a known selectivity.
        Assert.Contains(profile.Entries, e => e.IndexTable.StartsWith("IndexString:prefix", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProfileAsync_ProfilesDateRanges()
    {
        CorpusProfile profile = await Create().ProfileAsync("hash", 50, CancellationToken.None);

        Assert.NotEmpty(profile.DateRanges);
        Assert.All(profile.DateRanges, r => Assert.True(r.MinUtc <= r.MaxUtc));
    }

    [Fact]
    public async Task ProfileAsync_ProfilesReferenceFanOut()
    {
        CorpusProfile profile = await Create().ProfileAsync("hash", 50, CancellationToken.None);

        ReferenceFanOut subject = profile.ReferenceFanOuts.First(f => f.SearchParameterCode == "subject");

        Assert.Equal(2, subject.DistinctTargets);
        Assert.Equal(10, subject.MaxReferrers);
    }

    [Fact]
    public async Task ProfileAsync_CarriesTheCorpusIdentityHash()
    {
        CorpusProfile profile = await Create().ProfileAsync("abc123", 50, CancellationToken.None);

        Assert.Equal("abc123", profile.CorpusIdentityHash);
    }

    [Fact]
    public async Task ProfileAsync_RoundTripsThroughTheStore()
    {
        CorpusProfile profile = await Create().ProfileAsync("hash", 50, CancellationToken.None);
        string path = Path.Combine(_dir, "profile.json");

        await ProfileStore.WriteAsync(profile, path);
        CorpusProfile read = await ProfileStore.ReadAsync(path);

        Assert.Equal(profile.Entries.Count, read.Entries.Count);
        Assert.Equal(profile.CorpusIdentityHash, read.CorpusIdentityHash);
    }
}
```

- [ ] **Step 2: Run the profiler tests and verify they fail**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~CorpusProfilerTest"`
Expected: FAIL — profiling types do not exist.

- [ ] **Step 3: Implement the profile records and store**

`src/Abm.Pyro.Performance/Profiling/ProfileEntry.cs`:

```csharp
namespace Abm.Pyro.Performance.Profiling;

/// <summary>One indexed value and how much of its resource type it matches.</summary>
public record ValueFrequency(string Value, long Count, double Selectivity);

/// <summary>
/// The value distribution for one search parameter in one index table. Replaces the superseded
/// design's hand-specified distributions: the skew is discovered, not asserted (spec §11).
/// </summary>
public record ProfileEntry(
    string IndexTable,
    int SearchParameterStoreId,
    string SearchParameterCode,
    string ResourceTypeName,
    long ResourceTypeTotal,
    IReadOnlyList<ValueFrequency> Values);

public record DateRangeProfile(
    string SearchParameterCode,
    string ResourceTypeName,
    DateTime MinUtc,
    DateTime MaxUtc,
    IReadOnlyList<DateTime> DecileBoundariesUtc);

public record ReferenceFanOut(
    string SearchParameterCode,
    string ResourceTypeName,
    long DistinctTargets,
    long MaxReferrers,
    long MedianReferrers);
```

`src/Abm.Pyro.Performance/Profiling/CorpusProfile.cs`:

```csharp
using System.Text.Json;

namespace Abm.Pyro.Performance.Profiling;

public record CorpusProfile(
    string CorpusIdentityHash,
    DateTimeOffset ProfiledAtUtc,
    IReadOnlyList<ProfileEntry> Entries,
    IReadOnlyList<DateRangeProfile> DateRanges,
    IReadOnlyList<ReferenceFanOut> ReferenceFanOuts);

public static class ProfileStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static async Task WriteAsync(CorpusProfile profile, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(profile, Options));
    }

    public static async Task<CorpusProfile> ReadAsync(string path) =>
        JsonSerializer.Deserialize<CorpusProfile>(await File.ReadAllTextAsync(path), Options)
        ?? throw new InvalidOperationException($"Corpus profile at '{path}' deserialised to null.");
}
```

- [ ] **Step 4: Implement `CorpusProfiler`**

The profiler is one class issuing a handful of aggregate queries. The shape for `IndexToken`, which the others follow:

```sql
WITH TypeTotals AS (
    SELECT [ResourceType], COUNT(*) AS [Total]
    FROM [ResourceStore]
    WHERE [IsCurrent] = 1 AND [IsDeleted] = 0
    GROUP BY [ResourceType]
)
SELECT
    sp.[SearchParameterStoreId],
    sp.[Code],
    rs.[ResourceType],
    tt.[Total],
    COALESCE(it.[System], '') + '|' + COALESCE(it.[Code], '') AS [Value],
    COUNT(*) AS [Count]
FROM [IndexToken] it
INNER JOIN [ResourceStore] rs        ON rs.[ResourceStoreId] = it.[ResourceStoreId]
INNER JOIN [SearchParameterStore] sp ON sp.[SearchParameterStoreId] = it.[SearchParameterStoreId]
INNER JOIN TypeTotals tt             ON tt.[ResourceType] = rs.[ResourceType]
WHERE rs.[IsCurrent] = 1 AND rs.[IsDeleted] = 0
GROUP BY sp.[SearchParameterStoreId], sp.[Code], rs.[ResourceType], tt.[Total],
         COALESCE(it.[System], '') + '|' + COALESCE(it.[Code], '')
ORDER BY sp.[SearchParameterStoreId], COUNT(*) DESC;
```

Write `CorpusProfiler` with one private method per profiled table, each returning `IEnumerable<ProfileEntry>`:

- `ProfileTokenAsync()` — the query above. `Value` is `system|code`.
- `ProfileStringAsync()` — the same shape against `[IndexString]` with `[Value]` as the value, **plus** one pass per prefix length 1 through 5 using `LEFT([Value], n)`, recorded with `IndexTable = $"IndexString:prefix{n}"`. Prefix selectivity is what matters because string search is prefix-based (spec §11).
- `ProfileQuantityAsync()` — group by `COALESCE([System],'') + '|' + COALESCE([Code],'')` and additionally compute `[Quantity]` deciles per `(System, Code)` with `PERCENTILE_CONT`.
- `ProfileDateRangesAsync()` — `MIN([LowUtc])`, `MAX([HighUtc])` and nine decile boundaries per search parameter via `PERCENTILE_DISC(0.1) … (0.9) WITHIN GROUP (ORDER BY [LowUtc])`. Note the real column names are `LowUtc` and `HighUtc`, not `Low`/`High`.
- `ProfileReferenceFanOutAsync()` — group `[IndexReference]` by `([ResourceType], [ResourceId])` to get referrers per target, then aggregate to distinct targets, max and median.

Cap each entry's `Values` list at `topValuesPerParameter`, since a 2,000-code tail does not need committing. Set `Selectivity = (double)Count / ResourceTypeTotal`.

`IndexUri` and `IndexPosition` are **not** profiled — spec §14.1 records that this corpus populates neither, and profiling an empty table would produce entries no query-set role could use.

- [ ] **Step 5: Run the profiler tests and verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~CorpusProfilerTest"`
Expected: PASS — 8 tests.

- [ ] **Step 6: Implement the `profile` command and wire it in**

`src/Abm.Pyro.Performance/Cli/ProfileCommand.cs` resolves the manifest from the database (so the profile carries the right corpus identity), runs the profiler, and writes `assets/perf/corpus-profile.json`:

```csharp
using Abm.Pyro.Performance.Configuration;
using Abm.Pyro.Performance.Corpus;
using Abm.Pyro.Performance.Hosting;
using Abm.Pyro.Performance.Loading;
using Abm.Pyro.Performance.Profiling;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Cli;

public static class ProfileCommand
{
    public const string ProfilePath = "assets/perf/corpus-profile.json";

    public static async Task<CommandResult> RunAsync(ArgumentParser parser, PerformanceSettings settings)
    {
        await using PerformanceHost host = await PerformanceHost.StartAsync(settings);

        CorpusManifest manifest = await new ManifestStore(host.ConnectionString).ReadAsync()
            ?? throw new InvalidOperationException(
                "The database holds no corpus manifest. Run 'load' before 'profile'.");

        CorpusProfile profile = await new CorpusProfiler(host.ConnectionString).ProfileAsync(
            manifest.CorpusIdentityHash,
            topValuesPerParameter: parser.GetInt("top") ?? 50,
            CancellationToken.None);

        await ProfileStore.WriteAsync(profile, ProfilePath);

        return CommandResult.Ok(
            $"Profiled {profile.Entries.Count} parameter/table combinations, " +
            $"{profile.DateRanges.Count} date ranges and {profile.ReferenceFanOuts.Count} reference fan-outs " +
            $"to {ProfilePath}.");
    }
}
```

Add `"profile" => await ProfileCommand.RunAsync(parser, settings),` to the `Program.cs` switch.

- [ ] **Step 7: Commit**

```bash
git add src/Abm.Pyro.Performance src/Abm.Pyro.Performance.Test
git commit -m "feat(perf): derive selectivity statistics from the loaded corpus

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 11: Value role resolution and the query set

Spec §11.1 and §14. Query values are declared by selectivity and resolved from the profile. Review Focus 3 lives here: a role no value satisfies must fail loudly.

**Files:**
- Create: `src/Abm.Pyro.Performance/Profiling/SelectivityRole.cs`
- Create: `src/Abm.Pyro.Performance/Profiling/ValueRoleResolver.cs`
- Create: `src/Abm.Pyro.Performance/Baselines/QueryDefinition.cs`
- Create: `src/Abm.Pyro.Performance/Baselines/QuerySetLoader.cs`
- Create: `assets/perf/queries/*.json` (the §14 query set)
- Test: `src/Abm.Pyro.Performance.Test/Profiling/ValueRoleResolverTest.cs`
- Test: `src/Abm.Pyro.Performance.Test/Baselines/QuerySetLoaderTest.cs`

**Interfaces:**
- Consumes: `CorpusProfile`, `ProfileEntry`, `ValueFrequency` (Task 10).
- Produces:
  - `enum SelectivityBand { Hot, Mid, Cold }` with target selectivities `0.10`, `0.01`, `0.0001`
  - `record SelectivityRole(string IndexTable, string SearchParameterCode, string ResourceTypeName, SelectivityBand? Band, double? TargetSelectivity)`
  - `record ResolvedValue(string Literal, double AchievedSelectivity, long MatchingRows)`
  - `class ValueRoleResolver(CorpusProfile profile)` with `ResolvedValue Resolve(SelectivityRole role)` and `IReadOnlyList<ResolvedValue> ResolveSweep(SelectivityRole role, int points)`
  - `class RoleUnsatisfiableException : Exception`
  - `record QueryDefinition(string Id, string ResourceTypeName, string QueryTemplate, SelectivityRole? Value, bool Sweep)`
  - `static class QuerySetLoader` → `Task<IReadOnlyList<QueryDefinition>> LoadAsync(string directory)`

- [ ] **Step 1: Write the failing resolver tests**

`src/Abm.Pyro.Performance.Test/Profiling/ValueRoleResolverTest.cs`:

```csharp
using Abm.Pyro.Performance.Profiling;

namespace Abm.Pyro.Performance.Test.Profiling;

public class ValueRoleResolverTest
{
    private static CorpusProfile Profile(params (string Value, long Count)[] values)
    {
        long total = 1000;

        return new CorpusProfile(
            CorpusIdentityHash: "hash",
            ProfiledAtUtc: DateTimeOffset.UnixEpoch,
            Entries:
            [
                new ProfileEntry(
                    IndexTable: "IndexToken",
                    SearchParameterStoreId: 1,
                    SearchParameterCode: "code",
                    ResourceTypeName: "Observation",
                    ResourceTypeTotal: total,
                    Values: values
                        .Select(v => new ValueFrequency(v.Value, v.Count, (double)v.Count / total))
                        .OrderByDescending(v => v.Count)
                        .ToList()),
            ],
            DateRanges: [],
            ReferenceFanOuts: []);
    }

    private static SelectivityRole Role(SelectivityBand band) =>
        new("IndexToken", "code", "Observation", band, null);

    [Fact]
    public void Resolve_HotPicksTheValueNearestTenPercent()
    {
        var resolver = new ValueRoleResolver(Profile(("a", 300), ("b", 100), ("c", 1)));

        ResolvedValue result = resolver.Resolve(Role(SelectivityBand.Hot));

        Assert.Equal("b", result.Literal);
        Assert.Equal(0.1, result.AchievedSelectivity, precision: 4);
        Assert.Equal(100, result.MatchingRows);
    }

    [Fact]
    public void Resolve_ColdPicksTheRarestAvailableValue()
    {
        var resolver = new ValueRoleResolver(Profile(("a", 300), ("b", 100), ("c", 1)));

        Assert.Equal("c", resolver.Resolve(Role(SelectivityBand.Cold)).Literal);
    }

    [Fact]
    public void Resolve_MidPicksTheValueNearestOnePercent()
    {
        var resolver = new ValueRoleResolver(Profile(("a", 300), ("b", 100), ("c", 10), ("d", 1)));

        Assert.Equal("c", resolver.Resolve(Role(SelectivityBand.Mid)).Literal);
    }

    [Fact]
    public void Resolve_AnExplicitTargetSelectivityWins()
    {
        var resolver = new ValueRoleResolver(Profile(("a", 300), ("b", 100), ("c", 10)));

        var role = new SelectivityRole("IndexToken", "code", "Observation", null, TargetSelectivity: 0.3);

        Assert.Equal("a", resolver.Resolve(role).Literal);
    }

    // Review Focus 3 — a role no value satisfies must fail, not silently pick the nearest.
    [Fact]
    public void Resolve_ThrowsWhenNoValueIsWithinToleranceOfTheTarget()
    {
        // Hottest value matches 0.5%; a Hot role wants ~10%, an order of magnitude away.
        var resolver = new ValueRoleResolver(Profile(("a", 5), ("b", 1)));

        var ex = Assert.Throws<RoleUnsatisfiableException>(() => resolver.Resolve(Role(SelectivityBand.Hot)));

        Assert.Contains("Hot", ex.Message);
        Assert.Contains("code", ex.Message);
        Assert.Contains("0.5", ex.Message);
    }

    [Fact]
    public void Resolve_ThrowsWhenTheParameterIsNotInTheProfileAtAll()
    {
        var resolver = new ValueRoleResolver(Profile(("a", 100)));

        var role = new SelectivityRole("IndexToken", "not-profiled", "Observation", SelectivityBand.Hot, null);

        var ex = Assert.Throws<RoleUnsatisfiableException>(() => resolver.Resolve(role));

        Assert.Contains("not-profiled", ex.Message);
    }

    [Fact]
    public void Resolve_ThrowsWhenTheProfileEntryHasNoValues()
    {
        var resolver = new ValueRoleResolver(Profile());

        Assert.Throws<RoleUnsatisfiableException>(() => resolver.Resolve(Role(SelectivityBand.Hot)));
    }

    [Fact]
    public void ResolveSweep_ReturnsDistinctValuesSpanningTheSelectivityRange()
    {
        var resolver = new ValueRoleResolver(
            Profile(("a", 300), ("b", 100), ("c", 30), ("d", 10), ("e", 3), ("f", 1)));

        IReadOnlyList<ResolvedValue> sweep = resolver.ResolveSweep(Role(SelectivityBand.Hot), points: 4);

        Assert.Equal(4, sweep.Count);
        Assert.Equal(4, sweep.Select(v => v.Literal).Distinct().Count());
        Assert.True(sweep[0].AchievedSelectivity > sweep[^1].AchievedSelectivity);
    }

    [Fact]
    public void ResolveSweep_ThrowsWhenTheCorpusHasFewerValuesThanPointsRequested()
    {
        var resolver = new ValueRoleResolver(Profile(("a", 300), ("b", 100)));

        Assert.Throws<RoleUnsatisfiableException>(() => resolver.ResolveSweep(Role(SelectivityBand.Hot), points: 5));
    }
}
```

- [ ] **Step 2: Run the resolver tests and verify they fail**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~ValueRoleResolverTest"`
Expected: FAIL — resolver types do not exist.

- [ ] **Step 3: Implement `SelectivityRole` and `ValueRoleResolver`**

`src/Abm.Pyro.Performance/Profiling/SelectivityRole.cs`:

```csharp
namespace Abm.Pyro.Performance.Profiling;

/// <summary>
/// Three bands, not two: two points can only show that a plan flip exists, they cannot locate
/// it (spec §11.1).
/// </summary>
public enum SelectivityBand
{
    Hot,
    Mid,
    Cold,
}

public record SelectivityRole(
    string IndexTable,
    string SearchParameterCode,
    string ResourceTypeName,
    SelectivityBand? Band,
    double? TargetSelectivity)
{
    public double Target => TargetSelectivity ?? Band switch
    {
        SelectivityBand.Hot => 0.10,
        SelectivityBand.Mid => 0.01,
        SelectivityBand.Cold => 0.0001,
        _ => throw new ArgumentException("A SelectivityRole needs either a Band or a TargetSelectivity."),
    };

    public string Describe() =>
        $"{ResourceTypeName}.{SearchParameterCode} ({IndexTable}) " +
        $"{Band?.ToString() ?? $"target {Target:P4}"}";
}
```

`src/Abm.Pyro.Performance/Profiling/ValueRoleResolver.cs`:

```csharp
namespace Abm.Pyro.Performance.Profiling;

public class RoleUnsatisfiableException(string message) : Exception(message);

public record ResolvedValue(string Literal, double AchievedSelectivity, long MatchingRows);

/// <summary>
/// Turns "a value matching about 10% of this type" into the literal the corpus actually
/// contains, plus the selectivity it achieved. Both go into the baseline, so an entry stays
/// readable a year later without the profile beside it (spec §11.1).
/// </summary>
public class ValueRoleResolver(CorpusProfile profile)
{
    /// <summary>
    /// How far from the requested selectivity a value may sit and still count. A factor of 5
    /// either way: wide enough that a real corpus can satisfy a band, narrow enough that
    /// "hot" never silently resolves to a near-unique value.
    /// </summary>
    private const double ToleranceFactor = 5.0;

    public ResolvedValue Resolve(SelectivityRole role)
    {
        ProfileEntry entry = FindEntry(role);

        ValueFrequency nearest = entry.Values
            .OrderBy(v => Math.Abs(Math.Log(v.Selectivity) - Math.Log(role.Target)))
            .First();

        double ratio = nearest.Selectivity / role.Target;

        if (ratio is > ToleranceFactor or < 1 / ToleranceFactor)
        {
            throw new RoleUnsatisfiableException(
                $"No value for {role.Describe()} is within a factor of {ToleranceFactor:N0} of the " +
                $"requested selectivity {role.Target:P4}. The nearest is '{nearest.Value}' at " +
                $"{nearest.Selectivity:P4} ({nearest.MatchingRowsText()}). " +
                "Measuring it would record a number nobody can interpret as that band, so the query " +
                "set must be changed or the band dropped for this parameter.");
        }

        return new ResolvedValue(nearest.Value, nearest.Selectivity, nearest.Count);
    }

    public IReadOnlyList<ResolvedValue> ResolveSweep(SelectivityRole role, int points)
    {
        ProfileEntry entry = FindEntry(role);

        if (entry.Values.Count < points)
        {
            throw new RoleUnsatisfiableException(
                $"A {points}-point selectivity sweep of {role.Describe()} is impossible: the corpus " +
                $"holds only {entry.Values.Count} distinct values for it.");
        }

        // Even steps through the ranked list, so the sweep spans the whole range rather than
        // clustering at the hot end where most of the rows are.
        List<ValueFrequency> ranked = entry.Values.OrderByDescending(v => v.Count).ToList();

        return Enumerable.Range(0, points)
            .Select(i => ranked[(int)Math.Round((double)i * (ranked.Count - 1) / (points - 1))])
            .Select(v => new ResolvedValue(v.Value, v.Selectivity, v.Count))
            .ToList();
    }

    private ProfileEntry FindEntry(SelectivityRole role)
    {
        ProfileEntry? entry = profile.Entries.FirstOrDefault(e =>
            e.IndexTable == role.IndexTable &&
            e.SearchParameterCode == role.SearchParameterCode &&
            e.ResourceTypeName == role.ResourceTypeName);

        if (entry is null)
        {
            throw new RoleUnsatisfiableException(
                $"The corpus profile has no entry for {role.Describe()}. " +
                "Either the parameter is not indexed in this corpus or 'profile' has not been re-run " +
                "since the query set changed.");
        }

        if (entry.Values.Count == 0)
        {
            throw new RoleUnsatisfiableException(
                $"The corpus profile entry for {role.Describe()} holds no values, so nothing can be measured.");
        }

        return entry;
    }
}

internal static class ValueFrequencyExtensions
{
    public static string MatchingRowsText(this ValueFrequency value) => $"{value.Count:N0} rows";
}
```

- [ ] **Step 4: Run the resolver tests and verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~ValueRoleResolverTest"`
Expected: PASS — 9 tests.

- [ ] **Step 5: Write the query set and its loader test**

`src/Abm.Pyro.Performance/Baselines/QueryDefinition.cs`:

```csharp
using Abm.Pyro.Performance.Profiling;

namespace Abm.Pyro.Performance.Baselines;

/// <summary>
/// One access pattern from spec §14. QueryTemplate contains "{value}" where the resolved
/// literal is substituted; a definition with no Value is a query whose shape needs no
/// corpus-dependent literal (paging, _include, the count-versus-page pair).
/// </summary>
public record QueryDefinition(
    string Id,
    string ResourceTypeName,
    string QueryTemplate,
    SelectivityRole? Value,
    bool Sweep = false,
    int SweepPoints = 12);
```

`src/Abm.Pyro.Performance/Baselines/QuerySetLoader.cs` deserialises every `*.json` under the directory into `QueryDefinition[]`, failing loudly on a duplicate `Id` and on a template that contains `{value}` with no `Value` role (or a `Value` role with no `{value}` placeholder) — both are query-set bugs that would otherwise measure the wrong thing.

Write the query set across these files under `assets/perf/queries/`, one per access-pattern group from spec §14:

| File | Covers |
|---|---|
| `string.json` | prefix (default), `:exact`, `:contains` on `Patient.family` and `Patient.given`, at Hot/Mid/Cold |
| `token.json` | `Observation.code` code-only, system+code, system-only, `:not`, at Hot/Mid/Cold |
| `token-sweep.json` | the `Observation.code` 12-point selectivity sweep (`Sweep: true`) — the §2.2d crossover |
| `missing.json` | `:missing=true` and `:missing=false` on string, token, reference, date and quantity |
| `reference.json` | `Observation.subject` direct, and the multi-id `IN` fast path |
| `chained.json` | `Observation?subject.family=`, and `_has` (`Patient?_has:Observation:subject:code=`) |
| `date.json` | `Observation.date` `eq`, `ge`/`le` range, `gt`/`lt`, placed at decile boundaries |
| `quantity.json` | `Observation.value-quantity` with code, at Hot/Mid/Cold |
| `paging.json` | `_count` at page 1 and at a deep page |
| `include.json` | `_include=Observation:subject` |
| `count-vs-page.json` | the pair that exposes the §2.2c double execution |

An example entry, so the shape is unambiguous — `assets/perf/queries/token.json`:

```json
[
  {
    "Id": "token.code.hot",
    "ResourceTypeName": "Observation",
    "QueryTemplate": "code={value}",
    "Value": {
      "IndexTable": "IndexToken",
      "SearchParameterCode": "code",
      "ResourceTypeName": "Observation",
      "Band": "Hot"
    }
  },
  {
    "Id": "token.code.cold",
    "ResourceTypeName": "Observation",
    "QueryTemplate": "code={value}",
    "Value": {
      "IndexTable": "IndexToken",
      "SearchParameterCode": "code",
      "ResourceTypeName": "Observation",
      "Band": "Cold"
    }
  },
  {
    "Id": "token.code.not.hot",
    "ResourceTypeName": "Observation",
    "QueryTemplate": "code:not={value}",
    "Value": {
      "IndexTable": "IndexToken",
      "SearchParameterCode": "code",
      "ResourceTypeName": "Observation",
      "Band": "Hot"
    }
  }
]
```

`src/Abm.Pyro.Performance.Test/Baselines/QuerySetLoaderTest.cs` asserts: every committed file deserialises; ids are unique across the whole set; every definition with a `{value}` placeholder has a `Value` role and vice versa; and the set contains at least one entry per group in the table above (assert on id prefixes, so adding entries does not break the test).

- [ ] **Step 6: Run the query set tests and verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~QuerySetLoaderTest"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Abm.Pyro.Performance src/Abm.Pyro.Performance.Test assets/perf/queries
git commit -m "feat(perf): declare query values by selectivity and commit the query set

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 12: Baselines, the mutation guard, and `run`

Spec §10.5, §12.1, §15. Every entry carries logical reads, plan operator, elapsed median/p95, result cardinality, and the resolved literal with its achieved selectivity.

**Files:**
- Create: `src/Abm.Pyro.Performance/Baselines/BaselineEntry.cs`
- Create: `src/Abm.Pyro.Performance/Baselines/Baseline.cs`
- Create: `src/Abm.Pyro.Performance/Baselines/BaselineStore.cs`
- Create: `src/Abm.Pyro.Performance/Cli/RunCommand.cs`
- Modify: `src/Abm.Pyro.Performance/Program.cs`
- Test: `src/Abm.Pyro.Performance.Test/Baselines/BaselineStoreTest.cs`
- Test: `src/Abm.Pyro.Performance.Test/Integration/RunCommandTest.cs`

**Interfaces:**
- Consumes: everything from Tasks 5, 8, 9, 10, 11.
- Produces:
  - `record BaselineEntry(string QueryId, string ResourceTypeName, string ResolvedQueryString, string? ResolvedLiteral, double? AchievedSelectivity, int ResultCardinality, int TotalCount, long TotalLogicalReads, IReadOnlyList<TableLogicalReads> LogicalReadsByTable, IReadOnlyList<string> PlanOperators, double ElapsedMedianMs, double ElapsedP95Ms, bool Cold, bool SuspiciousZeroRows)`
  - `record Baseline(string Name, string CorpusIdentityHash, bool FromRestoredSnapshot, bool MigratedAfterRestore, DateTimeOffset RunAtUtc, IReadOnlyList<string> CoverageLedger, IReadOnlyList<BaselineEntry> Entries)`
  - `static class BaselineStore` with `Task WriteAsync(Baseline, string directory)`, `Task<Baseline> ReadAsync(string path)`, `void AssertComparable(Baseline a, Baseline b)`
  - `class BaselineMismatchException : Exception`

- [ ] **Step 1: Write the failing baseline store tests**

`src/Abm.Pyro.Performance.Test/Baselines/BaselineStoreTest.cs` covers: round-trip through JSON; `AssertComparable` passing for two baselines with the same corpus identity hash; `AssertComparable` throwing a `BaselineMismatchException` naming both hashes when they differ (spec §19.5 — `compare` refuses across corpora); and `SuspiciousZeroRows` being set whenever `ResultCardinality == 0`, because a zero-row query's timings are meaningless (spec §16).

```csharp
using Abm.Pyro.Performance.Baselines;
using Abm.Pyro.Performance.Measurement;

namespace Abm.Pyro.Performance.Test.Baselines;

public class BaselineStoreTest : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pyro-perf-baseline").FullName;

    private static BaselineEntry Entry(string id, int cardinality = 42) => new(
        QueryId: id,
        ResourceTypeName: "Observation",
        ResolvedQueryString: "code=http://loinc.org|8480-6",
        ResolvedLiteral: "http://loinc.org|8480-6",
        AchievedSelectivity: 0.097,
        ResultCardinality: cardinality,
        TotalCount: cardinality,
        TotalLogicalReads: 1234,
        LogicalReadsByTable: [new TableLogicalReads("IndexToken", 1000, 0, 0)],
        PlanOperators: ["Index Seek on IX_IndexToken_Code"],
        ElapsedMedianMs: 12.5,
        ElapsedP95Ms: 19.0,
        Cold: false,
        SuspiciousZeroRows: cardinality == 0);

    private static Baseline Baseline(string name, string hash) => new(
        Name: name,
        CorpusIdentityHash: hash,
        FromRestoredSnapshot: true,
        MigratedAfterRestore: false,
        RunAtUtc: new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
        CoverageLedger: ["IndexUri: none — corpus populates no uri-type parameter"],
        Entries: [Entry("token.code.hot")]);

    [Fact]
    public async Task WriteAsync_ReadAsync_RoundTrips()
    {
        Baseline original = Baseline("pre-phase-b", new string('a', 64));

        await BaselineStore.WriteAsync(original, _dir);
        Baseline read = await BaselineStore.ReadAsync(Path.Combine(_dir, "pre-phase-b.json"));

        Assert.Equal(original.CorpusIdentityHash, read.CorpusIdentityHash);
        Assert.Equal(0.097, read.Entries.Single().AchievedSelectivity);
        Assert.Equal("Index Seek on IX_IndexToken_Code", read.Entries.Single().PlanOperators.Single());
        Assert.Single(read.CoverageLedger);
    }

    [Fact]
    public void AssertComparable_PassesForTheSameCorpus()
    {
        string hash = new('a', 64);

        BaselineStore.AssertComparable(Baseline("a", hash), Baseline("b", hash));
    }

    [Fact]
    public void AssertComparable_ThrowsAcrossDifferentCorpora()
    {
        var ex = Assert.Throws<BaselineMismatchException>(() => BaselineStore.AssertComparable(
            Baseline("a", new string('a', 64)),
            Baseline("b", new string('b', 64))));

        Assert.Contains("aaaaaaaa", ex.Message);
        Assert.Contains("bbbbbbbb", ex.Message);
    }

    [Fact]
    public void Entry_ZeroCardinalityIsFlaggedSuspicious()
    {
        Assert.True(Entry("x", cardinality: 0).SuspiciousZeroRows);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
```

- [ ] **Step 2: Run the baseline store tests and verify they fail, then implement until green**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~BaselineStoreTest"`
Expected: FAIL, then PASS — 4 tests.

`BaselineStore.AssertComparable` throws when `CorpusIdentityHash` differs, with both hashes in the message. `WriteAsync` writes `<directory>/<name>.json` with `WriteIndented = true` so baselines diff well in git (spec §5.3).

- [ ] **Step 3: Write the failing `run` integration test**

`src/Abm.Pyro.Performance.Test/Integration/RunCommandTest.cs` asserts the three behaviours that make a baseline trustworthy:

```csharp
[Fact]
public async Task RunAsync_ProducesABaselineEntryPerQuery() { /* loads, profiles, runs a 2-entry query set, asserts 2 entries each with non-zero TotalLogicalReads and a non-empty PlanOperators list */ }

// Spec §10.5 — enforced, not documented.
[Fact]
public async Task RunAsync_RefusesWhenTheDatabaseHasBeenMutatedSinceTheSnapshot()
{
    // load, snapshot, then insert one resource via the create path
    var ex = await Assert.ThrowsAsync<CorpusMutatedException>(() => RunAsync(...));
    Assert.Contains("reset", ex.Message);
}

[Fact]
public async Task RunAsync_RefusesWhenTheManifestHashDoesNotMatchTheCorpusDirectory()
{
    // load, then add a file to the corpus directory so the recomputed identity differs
    var ex = await Assert.ThrowsAsync<CorpusMutatedException>(() => RunAsync(...));
    Assert.Contains("load", ex.Message);
}
```

- [ ] **Step 4: Implement `RunCommand`**

The sequence, which is also the sequence the test drives:

1. Start the host; read the `CorpusManifest` from `ManifestStore`. No manifest → tell the user to `load`.
2. **Mutation guard** — compare the live `ResourceStore` count against `manifest.ResourceStoreRowCount`. Differ → `CorpusMutatedException` naming `reset`.
3. **Identity guard** — recompute `CorpusIdentity` from the configured directory and compare with `manifest.CorpusIdentityHash`. Differ → `CorpusMutatedException` naming `load`.
4. Read `assets/perf/corpus-profile.json`; assert its `CorpusIdentityHash` matches the manifest's, so a stale profile cannot be used.
5. For each `QueryDefinition`: resolve its `Value` role (or sweep) through `ValueRoleResolver`, substitute into `QueryTemplate`, run `QueryRunner.ExecuteAsync`, then `PlanAnalyser.AnalyseAsync` over each captured command, cold and warm.
6. Build one `BaselineEntry` per query (per sweep point for a sweep), flagging `SuspiciousZeroRows`.
7. Attach the coverage ledger from spec §14.1 to `Baseline.CoverageLedger`, so no later sub-project can mistake an unmeasured table for a measured one (spec §19.9).
8. Write to `assets/perf/baselines/<name>.json`.

Add `"run" => await RunCommand.RunAsync(parser, settings),` to the `Program.cs` switch.

- [ ] **Step 5: Run the `run` tests and verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~RunCommandTest"`
Expected: PASS — 3 tests.

- [ ] **Step 6: Commit**

```bash
git add src/Abm.Pyro.Performance src/Abm.Pyro.Performance.Test
git commit -m "feat(perf): write guarded baselines carrying cardinality and selectivity

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 13: Report writer and `compare`

**Files:**
- Create: `src/Abm.Pyro.Performance/Baselines/ReportWriter.cs`
- Create: `src/Abm.Pyro.Performance/Cli/CompareCommand.cs`
- Modify: `src/Abm.Pyro.Performance/Program.cs`
- Test: `src/Abm.Pyro.Performance.Test/Baselines/ReportWriterTest.cs`

**Interfaces:**
- Consumes: `Baseline`, `BaselineEntry`, `BaselineStore` (Task 12).
- Produces: `static class ReportWriter` → `string Write(Baseline from, Baseline to)`

- [ ] **Step 1: Write the failing report tests**

`src/Abm.Pyro.Performance.Test/Baselines/ReportWriterTest.cs` asserts:

- The report names both baselines and the shared corpus identity hash.
- A query whose logical reads fell appears with its percentage delta.
- **A query whose result cardinality changed is called out as a result-set change, not a performance delta** — spec §12.1's whole purpose. Assert the row carries an explicit marker (for example `⚠ cardinality 1200 → 340`) rather than only a timing delta.
- A query present in one baseline and not the other is listed as added or removed rather than silently dropped.
- A query whose plan operator changed (seek → scan) is called out.
- The coverage ledger is reproduced in the report.
- `Write` on two baselines from different corpora throws `BaselineMismatchException` (it calls `BaselineStore.AssertComparable` first).

- [ ] **Step 2: Run and implement until green**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~ReportWriterTest"`
Expected: FAIL, then PASS.

`CompareCommand` reads both baselines by name from `assets/perf/baselines/`, calls `ReportWriter.Write`, prints the markdown to stdout and writes it to `assets/perf/baselines/<from>-vs-<to>.md`. Per spec §20.1 it **reports only and always exits 0** on a regression — the only consumer is a developer reading the markdown, and there is no CI gate to fail. A malformed or missing baseline name exits 1.

Add `"compare" => await CompareCommand.RunAsync(parser, settings),` to the switch.

- [ ] **Step 3: Commit**

```bash
git add src/Abm.Pyro.Performance src/Abm.Pyro.Performance.Test
git commit -m "feat(perf): render a markdown delta between two baselines

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 14: `ingest`

Spec §10.6's second write measurement: steady-state single-resource creates against the fully loaded corpus, where index trees are deep and page splits are real.

**Files:**
- Create: `src/Abm.Pyro.Performance/Cli/IngestCommand.cs`
- Create: `src/Abm.Pyro.Performance/Loading/IngestMeasurement.cs`
- Modify: `src/Abm.Pyro.Performance/Program.cs`
- Test: `src/Abm.Pyro.Performance.Test/Integration/IngestCommandTest.cs`

**Interfaces:**
- Consumes: `TenantScope`, `BundleReader`, `CorpusManifest`, `TableSizeReporter`.
- Produces:
  - `record IngestResult(int ResourcesCreated, double Seconds, double ResourcesPerSecond, IReadOnlyList<TableSize> TableGrowthKb)`
  - `class IngestMeasurement(IServiceProvider rootServices, PerformanceSettings settings)` with `Task<IngestResult> MeasureAsync(string corpusDirectory, int count, CancellationToken ct)`

- [ ] **Step 1: Write the failing ingest tests**

`src/Abm.Pyro.Performance.Test/Integration/IngestCommandTest.cs` asserts:

- `MeasureAsync` with `count: 20` creates exactly 20 new `ResourceStore` rows on top of the loaded corpus.
- Resources are **drawn from the corpus**, not generated — assert the created resources' types all appear in the manifest's census.
- Each resource goes through `FhirCreateRequest` individually, so there are 20 separate commits. Assert via `HttpVerb == HttpVerbId.Post` on the new rows and `VersionId == 1`.
- `ResourcesPerSecond` is positive and `TableGrowthKb` records a non-zero growth for at least `ResourceStore`.
- `MeasureAsync` with `count: 0` throws `ArgumentOutOfRangeException`.
- **After an ingest, `RunCommand`'s mutation guard fires** — this is the cross-check that ties Task 12's guard to this task's mutation. Assert `CorpusMutatedException` naming `reset`.

- [ ] **Step 2: Run and implement until green**

`IngestMeasurement` reads bundles from the corpus directory in sorted order, flattens their entries into a resource stream, takes `count` of them, strips each resource's `Id` and any `urn:uuid` references it cannot resolve (an ingest measures the create path, not reference resolution), and dispatches one `FhirCreateRequest` per resource through `IRequestDispatcher`. It measures table sizes with `TableSizeReporter` before and after and reports the deltas.

`IngestCommand` prints the result and ends with the reminder that the corpus is now mutated and `reset` is required before the next `run`.

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~IngestCommandTest"`
Expected: PASS — 6 tests.

- [ ] **Step 3: Commit**

```bash
git add src/Abm.Pyro.Performance src/Abm.Pyro.Performance.Test
git commit -m "feat(perf): measure steady-state ingest against the full corpus

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 15: Index shape probe

Spec §12's microbenchmark layer — six column orders compared in minutes without touching C#. Explicitly a hypothesis generator, never the evidence of record.

**Files:**
- Create: `src/Abm.Pyro.Performance/Measurement/IndexShapeProbe.cs`
- Create: `src/Abm.Pyro.Performance/Cli/ProbeCommand.cs`
- Create: `assets/perf/probes/*.json`
- Modify: `src/Abm.Pyro.Performance/Program.cs`
- Test: `src/Abm.Pyro.Performance.Test/Integration/IndexShapeProbeTest.cs`

**Interfaces:**
- Consumes: `PlanAnalyser`, `CapturedCommand` (Tasks 7, 9).
- Produces:
  - `record IndexShapeCandidate(string Name, string CreateIndexSql, string DropIndexSql)`
  - `record ProbeDefinition(string Id, string Sql, IReadOnlyList<IndexShapeCandidate> Candidates)`
  - `record ProbeResult(string ProbeId, string CandidateName, QueryMetrics Metrics)`
  - `class IndexShapeProbe(string connectionString)` with `Task<IReadOnlyList<ProbeResult>> RunAsync(ProbeDefinition probe, int iterations, CancellationToken ct)`

- [ ] **Step 1: Write the failing probe test**

`src/Abm.Pyro.Performance.Test/Integration/IndexShapeProbeTest.cs` asserts:

- `RunAsync` with two candidate index shapes over a hand-written statement returns two `ProbeResult`s, each with a parsed plan and non-zero logical reads.
- **Every candidate index is dropped again afterwards**, even when the statement throws — assert via `sys.indexes` that the probe left no index behind. This matters more than the measurement: a probe that leaks an index silently changes every subsequent measurement in the session.
- A candidate whose `CreateIndexSql` fails reports the failure and still drops whatever it created.
- A `ProbeDefinition` with no candidates throws.

- [ ] **Step 2: Run and implement until green**

`IndexShapeProbe` runs, per candidate: create the index, `PlanAnalyser.AnalyseAsync` the probe statement, drop the index — the drop in a `finally` block. Results are printed as a table ordered by total logical reads.

The commands in `ProbeCommand`'s output must carry an explicit banner, because the distinction is load-bearing and easy to forget six weeks later:

> These figures are hypotheses, not evidence. Only the pipeline-driven baselines (`run`) reflect the SQL EF actually produces, and only they may be cited in a sub-project D decision.

Commit the first probe definition at `assets/perf/probes/index-direction.json`, encoding §2.2d's two candidate shapes on `IndexToken`: `(SearchParameterStoreId, Code) INCLUDE (ResourceStoreId)` against `(SearchParameterStoreId, ResourceStoreId, Code)`.

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "FullyQualifiedName~IndexShapeProbeTest"`
Expected: PASS — 4 tests.

- [ ] **Step 3: Run the whole suite and the CI build**

```bash
dotnet build src/Abm.Pyro.CI.slnf
dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj
```
Expected: build succeeded; all tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/Abm.Pyro.Performance src/Abm.Pyro.Performance.Test assets/perf/probes
git commit -m "feat(perf): compare candidate index shapes as hypothesis generators

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 16: End-to-end run against the real corpus, and document the measured figures

The spec's §13 first deliverable and §19's success criteria can only be discharged against the real 1,180-bundle corpus. This task produces the numbers the whole programme then rests on.

**Files:**
- Create: `assets/perf/corpus-manifest.json` (generated)
- Create: `assets/perf/corpus-profile.json` (generated)
- Create: `assets/perf/baselines/pre-phase-b.json` (generated)
- Modify: `docs/superpowers/specs/2026-10-01-fhir-search-perf-harness-design.md` (§5.1, §10.5 — replace estimates with measured figures)
- Create: `src/Abm.Pyro.Performance/README.md`

- [ ] **Step 1: Point the harness at the real corpus and load it**

```bash
export PYRO_PERF_CORPUS="/c/Temp/Pyroserver/performance-testing/synthea_sample_data_fhir_r4_sep2019/fhir"
dotnet run --project src/Abm.Pyro.Performance/Abm.Pyro.Performance.csproj -- load
```

Expect this to take a long time and to be the first honest measurement of how long. Record the reported wall-clock, throughput, census and per-table sizes. If it fails on the 42 MB / 18,488-entry bundle, that is a real finding: record the failure mode before working around it, and consider spec §20.3 (resumable load) answered in the affirmative.

- [ ] **Step 2: Snapshot, and record how long backup and restore take**

```bash
dotnet run --project src/Abm.Pyro.Performance/Abm.Pyro.Performance.csproj -- snapshot
dotnet run --project src/Abm.Pyro.Performance/Abm.Pyro.Performance.csproj -- reset
```

- [ ] **Step 3: Profile, and check every query-set role resolves**

```bash
dotnet run --project src/Abm.Pyro.Performance/Abm.Pyro.Performance.csproj -- profile
```

A `RoleUnsatisfiableException` here is expected at least once and is the point of Review Focus 3: the band is wrong for that parameter in this corpus. Fix the query set — change the band or drop that entry — and record why in the entry's `Id` or a sibling comment file. Do not widen `ToleranceFactor` to make a role resolve; that would hide exactly what this check exists to surface.

- [ ] **Step 4: Take the first baseline**

```bash
dotnet run --project src/Abm.Pyro.Performance/Abm.Pyro.Performance.csproj -- run --queries all --baseline pre-phase-b
```

- [ ] **Step 5: Verify reproducibility — spec §19.6**

```bash
dotnet run --project src/Abm.Pyro.Performance/Abm.Pyro.Performance.csproj -- reset
dotnet run --project src/Abm.Pyro.Performance/Abm.Pyro.Performance.csproj -- run --queries all --baseline pre-phase-b-repeat
dotnet run --project src/Abm.Pyro.Performance/Abm.Pyro.Performance.csproj -- compare --from pre-phase-b --to pre-phase-b-repeat
```

Expected: logical reads identical in every entry; elapsed medians within ±15%. If logical reads differ at all, stop — something is non-deterministic that the design says is not, and that must be understood before any sub-project D decision rests on these numbers.

- [ ] **Step 6: Answer §2.2d**

Read the `token.code.sweep.*` entries in the baseline. Either the plan operator changes from an `IndexToken`-driven seek to a `ResourceStore`-driven scan at some selectivity, or it does not. Write the answer — with the crossover selectivity, or the statement that there is none across the range the corpus offers — into `src/Abm.Pyro.Performance/README.md`. Either result discharges §19.8.

- [ ] **Step 7: Replace the spec's estimates with measured fact**

In `docs/superpowers/specs/2026-10-01-fhir-search-perf-harness-design.md`:
- §5.1: add the measured on-disk size, index row counts per table, and load wall-clock.
- §10.5: replace the `est.` durations for `snapshot` and `reset` with the measured ones.
- §13: note that the deliverable is discharged, with the date.

- [ ] **Step 8: Write the README**

`src/Abm.Pyro.Performance/README.md` covers: how to configure the corpus path; the `load → snapshot → profile → run` sequence and that `reset` precedes every `run` after an `ingest`; the measured figures from Step 1–2; the §2.2d answer from Step 6; the coverage ledger, stated plainly — **this harness provides no evidence about `IndexUri` or `IndexPosition`**; and the standing caveat that every write-cost figure excludes profile-validation cost.

- [ ] **Step 9: Commit**

```bash
git add assets/perf docs/superpowers/specs/2026-10-01-fhir-search-perf-harness-design.md src/Abm.Pyro.Performance/README.md
git commit -m "feat(perf): first measured baseline against the full Synthea corpus

Replaces the spec's estimated figures with measured fact and records the
answer to the index-direction question of spec 2.2d.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Self-review

**1. Spec coverage.** Every section of Part II maps to a task:

| Spec | Task |
|---|---|
| §6 scope, §7 project shape and components | 1 (scaffold, .slnf), components spread across 2–15 |
| §8 corpus contract, `--max-files`, corpus identity | 1, 2 |
| §9 the loader, in-process dispatch, preconditions | 3, 4, 5 |
| §10.1–10.2 snapshot mechanism and connection drain | 6 |
| §10.3 why not database snapshots | 6 (documented in code, rationale in the spec) |
| §10.4 manifest in the database | 5 |
| §10.5 lifecycle and the mutation guard | 6, 12, 14 |
| §10.6 two write measurements | 5 (load throughput), 14 (ingest) |
| §11 profiler, §11.1 roles and the sweep | 10, 11 |
| §12 measurement, §12.1 cardinality | 7, 8, 9, 12 |
| §13 first deliverable | 5 (reporting), 16 (against the real corpus) |
| §14 query set, §14.1 coverage ledger | 11 (set), 12 (ledger in the baseline) |
| §15 reproducibility | 12 (recorded per baseline), 16 (verified) |
| §16 failure modes | 1, 2, 4, 5, 6, 7, 8, 9, 11, 12 — every row has a test |
| §17 testing the harness | tests throughout; the DB-free set is Tasks 1, 2, 7, 11, 12, 13 |
| §18 out of scope | no task touches `near`, `IndexUri`, a CI gate or a generator |
| §19 success criteria | 16 discharges 1–8; 9 is discharged by Task 12's ledger |
| §20 open items | 20.1 → Task 13 (report-only); 20.2 → Task 6; 20.3 → Task 16 Step 1 |

**2. Placeholders.** Tasks 1–11 carry complete code. Tasks 12–15 specify types, behaviours and test assertions in full but describe some bodies rather than printing them — `RunCommand`'s eight-step sequence, `CorpusProfiler`'s per-table queries beyond the worked `IndexToken` example, `ReportWriter`'s formatting, `IngestMeasurement`'s loop, `IndexShapeProbe`'s create/measure/drop. Each is a mechanical composition of types already defined with exact signatures, and each has its test assertions written out. This is deliberate: printing them in full would add length without adding decisions. **If an executing agent finds any of those under-specified, that is a plan defect — stop and ask rather than inventing a different design.**

**3. Type consistency.** Checked across tasks: `CapturedCommand`/`CapturedParameter` (7→9), `TableLogicalReads` (7→9→12), `PlanOperator` (7→9), `QueryMetrics` (9→12→15), `ProfileEntry`/`ValueFrequency` (10→11), `SelectivityRole` (11→12), `CorpusManifest` (1→5→10→12), `CorpusFile` (1→2→5), `TableSize` (5→14). `PerformanceHost.Interceptor` is added in Task 8 Step 3's note and consumed in Tasks 8 and 12. One naming hazard flagged: Task 10 uses the real column names `LowUtc`/`HighUtc`, not the spec's shorthand `Low`/`High`.

**4. Review Focus.** All five have an owning task and an explicit test: 1 → Task 2 (`Read_CollectionBundleThrowsNamingTheFile` and three siblings); 2 → Task 5 (`LoadAsync_AbortsWhenABundleFails`, plus the census-versus-row-count assertion inside `CorpusLoader`); 3 → Task 11 (`Resolve_ThrowsWhenNoValueIsWithinToleranceOfTheTarget`) and exercised for real in Task 16 Step 3; 4 → Task 6 (`RestoreAppliesMigrationsThatPostDateTheSnapshot`); 5 → Task 7 (`Parse_MalformedStatisticsLineThrows`, `Parse_MalformedXmlThrows`) and Task 9 (`AnalyseAsync_UnexecutableStatementThrows`).

**5. Six implementer notes** mark places where the plan asserts a codebase detail it could not fully verify without compiling: `NotificationManager`'s namespace (Task 3), Testcontainers member names (Task 3), `PyroDbContext` versus `IPyroDbContextFactory` resolution (Task 3), how to attach the interceptor to the real DbContext registration (Task 8), `InvalidQueryParameter`'s raw-text property (Task 8), and `sys.allocation_units.data_pages` (Task 5). Each note says what to check and what the requirement is independent of the mechanism. The interceptor one (Task 8) is the highest-risk: Tasks 9 and 12 both depend on it.


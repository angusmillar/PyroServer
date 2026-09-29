# FHIR Search Performance Measurement Harness (Sub-Project A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `src/Abm.Pyro.Performance`, a one-command reproducible measurement of every FHIR search access pattern against a deterministically generated 250k-resource corpus, capturing logical reads, plan operators and result cardinality into committed baselines.

**Architecture:** A .NET 10 console project hosts the **real** Pyro pipeline through `WebApplicationFactory<Program>` — the only way to obtain the genuine DI graph without duplicating `Program.cs`'s 498 lines of registration. A deterministic `CorpusGenerator` produces FHIR resources; the real `IIndexer` (the existing composition of all eight index setters) produces index rows; `SqlBulkCopy` writes them. A `QueryRunner` drives `ISearchQueryService` → `IResourceStoreSearch` and captures the emitted SQL with an EF command interceptor; `PlanAnalyser` re-executes that captured SQL under `SET STATISTICS IO, XML ON`. Nothing in the production projects changes.

**Tech Stack:** .NET 10, `Microsoft.AspNetCore.Mvc.Testing` (DI host), `Testcontainers.MsSql` 4.15.0, `Microsoft.Data.SqlClient` 7.0.3, `System.CommandLine`, `Hl7.Fhir.R4` 6.5.0, `NetTopologySuite` 2.6.0, xUnit 2.9.3.

**Spec:** `docs/superpowers/specs/2026-09-27-fhir-search-perf-harness-design.md` — Part II (§6–§17). Part I is programme context; this plan implements Part II only.

## Global Constraints

- **Target framework `net10.0`, `LangVersion 14`, `Nullable enable`, `ImplicitUsings enable`** — matching every other .NET 10 project in `src/`.
- **New projects MUST be added to `src/Abm.Pyro.CI.slnf`** (`CLAUDE.md`), or CI will not compile them. They must also be added to `src/Abm.Pyro.sln`.
- **Sub-project A changes no production behaviour** (spec §6). No file under `Abm.Pyro.Api`, `Abm.Pyro.Application`, `Abm.Pyro.Domain` or `Abm.Pyro.Repository` is modified by any task in this plan, with the single exception of adding `InternalsVisibleTo` if a task explicitly says so (no task does).
- **No EF migration is added.** Index changes belong to sub-project D.
- **Determinism is a hard requirement** (spec §8.1): no `DateTime.Now`, no `DateTime.UtcNow`, no `Guid.NewGuid()`, no unseeded `Random`, and no unordered dictionary iteration anywhere under `src/Abm.Pyro.Performance/Generation/`.
- **Corpus cache path defaults outside the repository tree** (spec §5.3): `%LOCALAPPDATA%\Pyro\perf-corpus\`. Git LFS is not used.
- **Working corpus scale is 250,000 resources; CI gate scale is 10,000** (spec §5.1). 500k and 1M are out of scope.
- **`using Task = System.Threading.Tasks.Task;`** in any file that also uses `Hl7.Fhir.Model` (`CLAUDE.md` coding conventions).
- **Docker must be running** for every task from Task 3 onward.

## Review Focus

Five input classes the spec implies but no task's own happy path exercises. Each has its test added to the owning task.

1. **`--scale 0` or a negative scale** — must fail with a named error before touching the database, not silently seed an empty corpus that then measures as "instant". *(Tasks 3 and 5)*
2. **Seeding over an already-populated database** — `SqlBulkCopy` with `KeepIdentity` into a non-empty `ResourceStore` yields a primary-key violation mid-write, leaving a half-seeded corpus whose hash still claims to be valid. Must detect and refuse before the first write. *(Task 5)*
3. **A query set entry naming an unsupported or misspelled search parameter** — `SearchQueryServiceOutcome.HasInvalidQuery` / `HasUnsupportedQuery` becomes true and the search returns *everything*, which reads as a spectacular performance result. Must fail loudly. *(Task 6)*
4. **Comparing two baselines taken at different scales or corpus hashes** — a cross-corpus diff is meaningless and must be refused, not rendered. *(Task 10)*
5. **A captured SQL statement that `PlanAnalyser` cannot re-execute or whose plan XML will not parse** — must fail loudly and never record a null, zero or defaulted metric (spec §13). *(Task 7)*

---

## File Structure

```
src/Abm.Pyro.Performance/
  Abm.Pyro.Performance.csproj
  Program.cs                              CLI root; subcommand wiring only
  Cli/
    SeedCommand.cs                        seed --scale --seed --profile --report
    RunCommand.cs                         run --queries --baseline
    CompareCommand.cs                     compare --from --to
    IngestCommand.cs                      ingest --count
    SnapshotCommand.cs                    snapshot export|import
  Generation/
    CorpusManifest.cs                     record + content hash
    DistributionProfile.cs                skew definitions (committed data)
    DeterministicRandom.cs                explicit seeded RNG
    CorpusGenerator.cs                    (seed, scale, profile) -> IEnumerable<Resource>
    PatientGenerator.cs                   per-type generators, one file each
    ObservationGenerator.cs
    EncounterGenerator.cs
    DiagnosticReportGenerator.cs
    LocationGenerator.cs
  Hosting/
    CorpusHost.cs                         container + WebApplicationFactory lifecycle
    PerformanceWebApplicationFactory.cs   DI host, no HTTP server needed
    TenantScope.cs                        sets the scoped tenant on a DI scope
  Seeding/
    BulkIndexWriter.cs                    IIndexer -> SqlBulkCopy (+ EF for IndexPosition)
    TableSizeReporter.cs                  sp_spaceused per table
  Measurement/
    CapturedCommand.cs                    record of one captured SQL statement
    SqlCaptureInterceptor.cs              DbCommandInterceptor
    QueryRunner.cs                        FHIR query string -> pipeline -> CapturedCommand[]
    PlanAnalyser.cs                       captured SQL -> logical reads + plan operators
    StatisticsIoParser.cs                 InfoMessage text -> per-table logical reads
    PlanXmlParser.cs                      plan XML -> operator list
    IndexShapeProbe.cs                    hand-written SQL microbenchmarks
  Baselines/
    BaselineEntry.cs                      one measured query
    Baseline.cs                           a whole run
    BaselineStore.cs                      JSON read/write
    QuerySetLoader.cs                     assets/perf/queries/*.json -> QueryDefinition[]
    ReportWriter.cs                       markdown comparison

src/Abm.Pyro.Performance.Test/
  Abm.Pyro.Performance.Test.csproj
  Generation/CorpusGeneratorTest.cs       determinism + distribution assertions
  Generation/CorpusManifestTest.cs        hash stability
  Measurement/StatisticsIoParserTest.cs   no database
  Measurement/PlanXmlParserTest.cs        against committed sample XML
  Baselines/BaselineStoreTest.cs          round-trip, cross-corpus refusal
  Baselines/ReportWriterTest.cs           markdown shape
  Integration/SmokeSeedAndRunTest.cs      1k seed -> one query -> baseline produced
  Integration/CiGateTest.cs               10k seek-not-scan gate
  Assets/sample-plan-seek.xml             committed plan XML fixtures
  Assets/sample-plan-scan.xml

assets/perf/
  corpus.json                             the committed manifest
  queries/*.json                          the query set (§12)
  baselines/*.json                        committed baseline runs
```

---

### Task 1: Project scaffold and the corpus manifest

The manifest is the corpus's identity (spec §5.3, §13). Building it first means every later task has something concrete to hash against.

**Files:**
- Create: `src/Abm.Pyro.Performance/Abm.Pyro.Performance.csproj`
- Create: `src/Abm.Pyro.Performance/Program.cs`
- Create: `src/Abm.Pyro.Performance/Generation/CorpusManifest.cs`
- Create: `src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj`
- Create: `src/Abm.Pyro.Performance.Test/Generation/CorpusManifestTest.cs`
- Modify: `src/Abm.Pyro.CI.slnf`
- Modify: `src/Abm.Pyro.sln`

**Interfaces:**
- Consumes: nothing.
- Produces: `CorpusManifest` record with `GeneratorVersion` (string), `Seed` (int), `Scale` (int), `Profile` (string), and `ContentHash` (string, computed property). `CorpusManifest.DefaultCachePath()` returns the out-of-repo cache directory. `CorpusManifest.CurrentGeneratorVersion` is the hand-bumped version constant.

- [ ] **Step 1: Create the two project files**

`src/Abm.Pyro.Performance/Abm.Pyro.Performance.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <LangVersion>14</LangVersion>
        <RootNamespace>Abm.Pyro.Performance</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
        <PackageReference Include="Testcontainers.MsSql" Version="4.15.0" />
        <PackageReference Include="Microsoft.Data.SqlClient" Version="7.0.3" />
        <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\Abm.Pyro.Api\Abm.Pyro.Api.csproj" />
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
        <None Update="Assets\**\*">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </None>
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\Abm.Pyro.Performance\Abm.Pyro.Performance.csproj" />
    </ItemGroup>

</Project>
```

- [ ] **Step 2: Write the failing manifest test**

`src/Abm.Pyro.Performance.Test/Generation/CorpusManifestTest.cs`:

```csharp
using Abm.Pyro.Performance.Generation;

namespace Abm.Pyro.Performance.Test.Generation;

public class CorpusManifestTest
{
    [Fact]
    public void ContentHash_IsStableForTheSameInputs()
    {
        var first = new CorpusManifest("1.0.0", Seed: 42, Scale: 250000, Profile: "default");
        var second = new CorpusManifest("1.0.0", Seed: 42, Scale: 250000, Profile: "default");

        Assert.Equal(first.ContentHash, second.ContentHash);
    }

    [Fact]
    public void ContentHash_IsALowercaseSha256()
    {
        var manifest = new CorpusManifest("1.0.0", Seed: 42, Scale: 250000, Profile: "default");

        Assert.Equal(64, manifest.ContentHash.Length);
        Assert.Matches("^[0-9a-f]{64}$", manifest.ContentHash);
    }

    [Theory]
    [InlineData("1.0.1", 42, 250000, "default")]
    [InlineData("1.0.0", 43, 250000, "default")]
    [InlineData("1.0.0", 42, 10000, "default")]
    [InlineData("1.0.0", 42, 250000, "sparse")]
    public void ContentHash_ChangesWhenAnyInputChanges(string version, int seed, int scale, string profile)
    {
        var baseline = new CorpusManifest("1.0.0", Seed: 42, Scale: 250000, Profile: "default");
        var altered = new CorpusManifest(version, seed, scale, profile);

        Assert.NotEqual(baseline.ContentHash, altered.ContentHash);
    }

    [Fact]
    public void DefaultCachePath_IsOutsideTheRepositoryTree()
    {
        string path = CorpusManifest.DefaultCachePath();

        Assert.Contains("Pyro", path);
        Assert.Contains("perf-corpus", path);
        Assert.DoesNotContain("PyroServer", path);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj`
Expected: FAIL — `CorpusManifest` does not exist.

- [ ] **Step 4: Implement `CorpusManifest`**

`src/Abm.Pyro.Performance/Generation/CorpusManifest.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Abm.Pyro.Performance.Generation;

/// <summary>
/// The corpus's identity. A generated corpus is a pure function of these four inputs, so the
/// corpus is reproducible rather than archived (spec §5.3). Every measurement records the hash,
/// and the harness refuses to measure a database whose stored manifest does not match the one
/// requested (spec §13).
/// </summary>
public sealed record CorpusManifest(string GeneratorVersion, int Seed, int Scale, string Profile)
{
    /// <summary>
    /// Bumped by hand whenever a change to the generator alters the resources it produces.
    /// Forgetting to bump it is the one way to silently invalidate a baseline, so any change
    /// under Generation/ must be accompanied by a bump here.
    /// </summary>
    public const string CurrentGeneratorVersion = "1.0.0";

    [JsonIgnore]
    public string ContentHash
    {
        get
        {
            string material = $"{GeneratorVersion}|{Seed}|{Scale}|{Profile}";
            byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));
            return Convert.ToHexStringLower(digest);
        }
    }

    /// <summary>
    /// Defaults outside the repository tree so a multi-gigabyte corpus cannot be reached by
    /// 'git add' from the repo root (spec §5.3). .gitignore is the second line of defence here,
    /// not the only one.
    /// </summary>
    public static string DefaultCachePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Pyro",
            "perf-corpus");
    }
}
```

- [ ] **Step 5: Write the CLI root**

`src/Abm.Pyro.Performance/Program.cs`:

```csharp
using System.CommandLine;

namespace Abm.Pyro.Performance;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var root = new RootCommand("Pyro FHIR search performance measurement harness");

        // Subcommands are added by later tasks: seed (Task 5), run (Task 9),
        // compare (Task 10), ingest (Task 11), snapshot (Task 14).

        return await root.InvokeAsync(args);
    }
}
```

- [ ] **Step 6: Add both projects to the solution and the CI filter**

Add these two lines to the `projects` array in `src/Abm.Pyro.CI.slnf`, after the `Abm.Pyro.Repository.Test` entry (remembering the comma on the preceding line):

```json
      "Abm.Pyro.Performance\\Abm.Pyro.Performance.csproj",
      "Abm.Pyro.Performance.Test\\Abm.Pyro.Performance.Test.csproj"
```

Then add them to the full solution:

```bash
cd src
dotnet sln Abm.Pyro.sln add Abm.Pyro.Performance/Abm.Pyro.Performance.csproj
dotnet sln Abm.Pyro.sln add Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj
```

- [ ] **Step 7: Run the tests to verify they pass, and build the filter**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj`
Expected: PASS — eight tests (five theory cases count individually).

Run: `dotnet build src/Abm.Pyro.CI.slnf`
Expected: succeeds, and the build output names both new projects.

- [ ] **Step 8: Commit**

```bash
git add src/Abm.Pyro.Performance src/Abm.Pyro.Performance.Test src/Abm.Pyro.CI.slnf src/Abm.Pyro.sln
git commit -m "feat: scaffold the performance harness project and corpus manifest

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 2: Deterministic RNG and distribution profile

Skew, not volume, is what separates a seek from a scan (spec §8.2). This task builds the skew machinery and pins it with distribution assertions, before any resource is generated.

**Files:**
- Create: `src/Abm.Pyro.Performance/Generation/DeterministicRandom.cs`
- Create: `src/Abm.Pyro.Performance/Generation/DistributionProfile.cs`
- Create: `src/Abm.Pyro.Performance.Test/Generation/DistributionProfileTest.cs`

**Interfaces:**
- Consumes: nothing from Task 1 except the project reference.
- Produces:
  - `DeterministicRandom(int seed)` with `int Next(int maxExclusive)`, `double NextDouble()`, `T Pick<T>(IReadOnlyList<T> items)`, `T PickZipf<T>(IReadOnlyList<T> items)`, `DateTime NextDateClusteredRecent(DateTime anchorUtc, int maxDaysBack)`.
  - `DistributionProfile` with `static DistributionProfile Default()`, `static readonly DateTime AnchorUtc`, and properties `Name`, `FamilyNames`, `GivenNames`, `ObservationCodes`, `TokenSystems`, `ObservationsPerPatient`.

- [ ] **Step 1: Write the failing distribution test**

`src/Abm.Pyro.Performance.Test/Generation/DistributionProfileTest.cs`:

```csharp
using Abm.Pyro.Performance.Generation;

namespace Abm.Pyro.Performance.Test.Generation;

public class DistributionProfileTest
{
    [Fact]
    public void Default_HasALongEnoughTailThatAColdCodeIsSelective()
    {
        DistributionProfile profile = DistributionProfile.Default();

        Assert.True(profile.ObservationCodes.Count >= 2000);
    }

    [Fact]
    public void PickZipf_ConcentratesRoughlyEightyPercentInTheTopTwenty()
    {
        // The index-direction question of spec §2.2d needs a hot code and a cold code that
        // behave oppositely. This asserts the corpus actually delivers that contrast.
        DistributionProfile profile = DistributionProfile.Default();
        var random = new DeterministicRandom(seed: 42);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < 100_000; i++)
        {
            string code = random.PickZipf(profile.ObservationCodes);
            counts[code] = counts.GetValueOrDefault(code) + 1;
        }

        int topTwenty = counts
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Take(20)
            .Sum(x => x.Value);

        double share = topTwenty / 100_000d;
        Assert.InRange(share, 0.70, 0.90);
    }

    [Fact]
    public void PickZipf_ReachesMuchOfTheColdTail()
    {
        DistributionProfile profile = DistributionProfile.Default();
        var random = new DeterministicRandom(seed: 42);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < 2_000_000; i++)
        {
            seen.Add(random.PickZipf(profile.ObservationCodes));
        }

        Assert.True(seen.Count > profile.ObservationCodes.Count / 2,
            "at least half the tail must be reachable, or cold-value queries measure nothing");
    }

    [Fact]
    public void DeterministicRandom_ProducesTheSameSequenceForTheSameSeed()
    {
        var first = new DeterministicRandom(seed: 7);
        var second = new DeterministicRandom(seed: 7);

        int[] firstDraws = Enumerable.Range(0, 1000).Select(_ => first.Next(1000)).ToArray();
        int[] secondDraws = Enumerable.Range(0, 1000).Select(_ => second.Next(1000)).ToArray();

        Assert.Equal(firstDraws, secondDraws);
    }

    [Fact]
    public void DeterministicRandom_ProducesDifferentSequencesForDifferentSeeds()
    {
        var first = new DeterministicRandom(seed: 7);
        var second = new DeterministicRandom(seed: 8);

        int[] firstDraws = Enumerable.Range(0, 1000).Select(_ => first.Next(1000)).ToArray();
        int[] secondDraws = Enumerable.Range(0, 1000).Select(_ => second.Next(1000)).ToArray();

        Assert.NotEqual(firstDraws, secondDraws);
    }

    [Fact]
    public void NextDateClusteredRecent_StaysWithinTheRequestedWindow()
    {
        var random = new DeterministicRandom(seed: 42);

        for (int i = 0; i < 10_000; i++)
        {
            DateTime value = random.NextDateClusteredRecent(DistributionProfile.AnchorUtc, maxDaysBack: 3650);

            Assert.True(value <= DistributionProfile.AnchorUtc);
            Assert.True(value >= DistributionProfile.AnchorUtc.AddDays(-3650));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter DistributionProfileTest`
Expected: FAIL — `DeterministicRandom` and `DistributionProfile` do not exist.

- [ ] **Step 3: Implement `DeterministicRandom`**

`src/Abm.Pyro.Performance/Generation/DeterministicRandom.cs`:

```csharp
namespace Abm.Pyro.Performance.Generation;

/// <summary>
/// The only source of randomness in the generator, threaded explicitly so that generation is a
/// pure function of the seed (spec §8.1). Wraps System.Random, whose seeded constructor uses an
/// algorithm that is stable for a given seed within a .NET major version.
/// </summary>
public sealed class DeterministicRandom(int seed)
{
    private readonly Random _random = new(seed);

    public int Next(int maxExclusive) => _random.Next(maxExclusive);

    public double NextDouble() => _random.NextDouble();

    public T Pick<T>(IReadOnlyList<T> items) => items[_random.Next(items.Count)];

    /// <summary>
    /// Zipf-like selection: low indexes are drawn far more often than high ones. The
    /// inverse-transform below is a closed-form approximation of the harmonic CDF, chosen
    /// because it is cheap enough to call tens of millions of times during a seed.
    /// </summary>
    public T PickZipf<T>(IReadOnlyList<T> items)
    {
        double u = _random.NextDouble();
        int index = (int)(Math.Pow(items.Count + 1, u) - 1);
        if (index < 0)
        {
            index = 0;
        }

        if (index >= items.Count)
        {
            index = items.Count - 1;
        }

        return items[index];
    }

    /// <summary>
    /// Dates clustered towards the anchor with a long tail backwards, which is what a real
    /// clinical corpus looks like and what makes a date range predicate interesting.
    /// </summary>
    public DateTime NextDateClusteredRecent(DateTime anchorUtc, int maxDaysBack)
    {
        double u = _random.NextDouble();
        int daysBack = (int)(maxDaysBack * u * u);
        return anchorUtc.AddDays(-daysBack);
    }
}
```

- [ ] **Step 4: Implement `DistributionProfile`**

`src/Abm.Pyro.Performance/Generation/DistributionProfile.cs`:

```csharp
namespace Abm.Pyro.Performance.Generation;

/// <summary>
/// The committed skew definitions. Each distribution exists to probe a specific index decision
/// (spec §8.2); none of it is decoration. Values are literals rather than generated at runtime so
/// the profile itself is part of the corpus identity and cannot drift.
/// </summary>
public sealed class DistributionProfile
{
    public required string Name { get; init; }

    /// <summary>Zipf-drawn. Probes IndexString prefix selectivity at both the hot and cold ends.</summary>
    public required IReadOnlyList<string> FamilyNames { get; init; }

    public required IReadOnlyList<string> GivenNames { get; init; }

    /// <summary>
    /// Zipf-drawn: ~20 codes cover ~80% of rows, with a tail of ~2,000. This is the distribution
    /// that answers the index-direction question of spec §2.2d, because a hot code plausibly wants
    /// ResourceStore-first and a cold code plausibly wants index-table-first.
    /// </summary>
    public required IReadOnlyList<string> ObservationCodes { get; init; }

    /// <summary>Heavily skewed to one system. Probes whether the System column earns index space.</summary>
    public required IReadOnlyList<string> TokenSystems { get; init; }

    /// <summary>Reference fan-out, driving _has, chained and _include costs.</summary>
    public required int ObservationsPerPatient { get; init; }

    /// <summary>
    /// The fixed instant every generated date is measured back from. A constant, never
    /// DateTime.UtcNow, because a corpus generated today and one generated next week must be
    /// identical (spec §8.1).
    /// </summary>
    public static readonly DateTime AnchorUtc = new(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc);

    public static DistributionProfile Default()
    {
        return new DistributionProfile
        {
            Name = "default",
            FamilyNames = BuildNameList(["Smith", "Jones", "Williams", "Brown", "Taylor"], tailLength: 500),
            GivenNames = BuildNameList(["James", "Mary", "Robert", "Patricia", "John"], tailLength: 300),
            ObservationCodes = BuildCodeList(2000),
            TokenSystems =
            [
                "http://loinc.org",
                "http://snomed.info/sct",
                "http://terminology.hl7.org/CodeSystem/observation-category",
                "http://hl7.org.au/fhir/CodeSystem/local",
            ],
            ObservationsPerPatient = 50,
        };
    }

    /// <summary>
    /// A handful of real-looking hot names followed by a deterministic synthetic tail. The tail is
    /// built from an index rather than a word list so the profile stays small in git while still
    /// giving a prefix search a genuinely long tail to miss on.
    /// </summary>
    private static IReadOnlyList<string> BuildNameList(IReadOnlyList<string> hotNames, int tailLength)
    {
        var names = new List<string>(tailLength + hotNames.Count);
        names.AddRange(hotNames);
        for (int i = 0; i < tailLength; i++)
        {
            names.Add($"Tailname{i:D4}");
        }

        return names;
    }

    private static IReadOnlyList<string> BuildCodeList(int count)
    {
        // The first twenty are the hot head; Zipf selection concentrates ~80% of draws there.
        var codes = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            codes.Add($"{10000 + i}-{i % 10}");
        }

        return codes;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter DistributionProfileTest`
Expected: PASS — six tests. If `PickZipf_ConcentratesRoughlyEightyPercentInTheTopTwenty` falls outside 0.70–0.90, adjust the exponent in `PickZipf` until it lands inside; do **not** widen the assertion range, because the 80/20 shape is the thing being guaranteed.

- [ ] **Step 6: Commit**

```bash
git add src/Abm.Pyro.Performance/Generation src/Abm.Pyro.Performance.Test/Generation
git commit -m "feat: deterministic RNG and the committed distribution profile

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: The corpus generator

`(seed, scale, profile)` → resources, as a pure function (spec §8). Five resource types, generated in a fixed order so the same seed produces byte-identical output.

**Files:**
- Create: `src/Abm.Pyro.Performance/Generation/CorpusGenerator.cs`
- Create: `src/Abm.Pyro.Performance/Generation/PatientGenerator.cs`
- Create: `src/Abm.Pyro.Performance/Generation/ObservationGenerator.cs`
- Create: `src/Abm.Pyro.Performance/Generation/EncounterGenerator.cs`
- Create: `src/Abm.Pyro.Performance/Generation/DiagnosticReportGenerator.cs`
- Create: `src/Abm.Pyro.Performance/Generation/LocationGenerator.cs`
- Create: `src/Abm.Pyro.Performance.Test/Generation/CorpusGeneratorTest.cs`

**Interfaces:**
- Consumes: `DeterministicRandom`, `DistributionProfile`, `CorpusManifest` (Tasks 1–2).
- Produces: `CorpusGenerator.Generate(CorpusManifest manifest, DistributionProfile profile)` returning `IEnumerable<GeneratedResource>`, where `GeneratedResource` is `record GeneratedResource(int ResourceStoreId, Resource Resource, FhirResourceTypeId ResourceType, DateTime LastUpdatedUtc)`. Ids are dense and 1-based so `SqlBulkCopy` can write them with `KeepIdentity` (Task 5). Also `CorpusGenerator.ComputeContentHash(IEnumerable<GeneratedResource>)` returning a lowercase SHA-256 over the serialised resources, used by the determinism test.

- [ ] **Step 1: Write the failing generator test**

`src/Abm.Pyro.Performance.Test/Generation/CorpusGeneratorTest.cs`:

```csharp
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Performance.Generation;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Performance.Test.Generation;

public class CorpusGeneratorTest
{
    private static readonly CorpusManifest SmallManifest =
        new(CorpusManifest.CurrentGeneratorVersion, Seed: 42, Scale: 1000, Profile: "default");

    [Fact]
    public void Generate_ProducesExactlyTheRequestedNumberOfResources()
    {
        List<GeneratedResource> corpus = CorpusGenerator.Generate(SmallManifest, DistributionProfile.Default()).ToList();

        Assert.Equal(1000, corpus.Count);
    }

    [Fact]
    public void Generate_IsDeterministic_SameSeedGivesTheSameContentHash()
    {
        string first = CorpusGenerator.ComputeContentHash(
            CorpusGenerator.Generate(SmallManifest, DistributionProfile.Default()));
        string second = CorpusGenerator.ComputeContentHash(
            CorpusGenerator.Generate(SmallManifest, DistributionProfile.Default()));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Generate_IsSensitiveToTheSeed()
    {
        var otherSeed = SmallManifest with { Seed = 43 };

        string first = CorpusGenerator.ComputeContentHash(
            CorpusGenerator.Generate(SmallManifest, DistributionProfile.Default()));
        string second = CorpusGenerator.ComputeContentHash(
            CorpusGenerator.Generate(otherSeed, DistributionProfile.Default()));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Generate_AssignsDenseOneBasedIds()
    {
        List<GeneratedResource> corpus = CorpusGenerator.Generate(SmallManifest, DistributionProfile.Default()).ToList();

        Assert.Equal(Enumerable.Range(1, 1000), corpus.Select(x => x.ResourceStoreId));
    }

    [Fact]
    public void Generate_ProducesAllFiveResourceTypes()
    {
        List<GeneratedResource> corpus = CorpusGenerator.Generate(SmallManifest, DistributionProfile.Default()).ToList();

        HashSet<FhirResourceTypeId> types = corpus.Select(x => x.ResourceType).ToHashSet();

        Assert.Contains(FhirResourceTypeId.Patient, types);
        Assert.Contains(FhirResourceTypeId.Observation, types);
        Assert.Contains(FhirResourceTypeId.Encounter, types);
        Assert.Contains(FhirResourceTypeId.DiagnosticReport, types);
        Assert.Contains(FhirResourceTypeId.Location, types);
    }

    [Fact]
    public void Generate_PointsEveryObservationAtAGeneratedPatient()
    {
        List<GeneratedResource> corpus = CorpusGenerator.Generate(SmallManifest, DistributionProfile.Default()).ToList();

        HashSet<string> patientReferences = corpus
            .Where(x => x.ResourceType == FhirResourceTypeId.Patient)
            .Select(x => $"Patient/{x.Resource.Id}")
            .ToHashSet(StringComparer.Ordinal);

        List<Observation> observations = corpus
            .Where(x => x.ResourceType == FhirResourceTypeId.Observation)
            .Select(x => (Observation)x.Resource)
            .ToList();

        Assert.NotEmpty(observations);
        Assert.All(observations, observation =>
            Assert.Contains(observation.Subject.Reference, patientReferences));
    }

    [Fact]
    public void Generate_GivesEveryLocationAPosition()
    {
        List<GeneratedResource> corpus = CorpusGenerator.Generate(SmallManifest, DistributionProfile.Default()).ToList();

        List<Location> locations = corpus
            .Where(x => x.ResourceType == FhirResourceTypeId.Location)
            .Select(x => (Location)x.Resource)
            .ToList();

        Assert.NotEmpty(locations);
        Assert.All(locations, location =>
        {
            Assert.NotNull(location.Position);
            Assert.NotNull(location.Position.Latitude);
            Assert.NotNull(location.Position.Longitude);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Generate_RefusesANonPositiveScale(int scale)
    {
        // Review Focus #1: a zero or negative scale must fail with a named error, not silently
        // produce an empty corpus that then measures as "instant" against every query.
        var manifest = SmallManifest with { Scale = scale };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CorpusGenerator.Generate(manifest, DistributionProfile.Default()).ToList());
    }

    [Fact]
    public void Generate_UsesNoWallClockTime()
    {
        // Every LastUpdatedUtc must derive from the profile anchor, never from the machine clock,
        // or a corpus reseeded tomorrow would differ from the one the baseline was taken against.
        List<GeneratedResource> corpus = CorpusGenerator.Generate(SmallManifest, DistributionProfile.Default()).ToList();

        Assert.All(corpus, resource =>
            Assert.True(resource.LastUpdatedUtc <= DistributionProfile.AnchorUtc));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter CorpusGeneratorTest`
Expected: FAIL — `CorpusGenerator` does not exist.

- [ ] **Step 3: Implement the per-type generators**

`src/Abm.Pyro.Performance/Generation/PatientGenerator.cs`:

```csharp
using Hl7.Fhir.Model;

namespace Abm.Pyro.Performance.Generation;

internal static class PatientGenerator
{
    public static Patient Create(string id, DeterministicRandom random, DistributionProfile profile)
    {
        return new Patient
        {
            Id = id,
            Active = true,
            Gender = random.Next(2) == 0 ? AdministrativeGender.Male : AdministrativeGender.Female,
            BirthDate = random
                .NextDateClusteredRecent(DistributionProfile.AnchorUtc, maxDaysBack: 36500)
                .ToString("yyyy-MM-dd"),
            Name =
            [
                new HumanName
                {
                    Family = random.PickZipf(profile.FamilyNames),
                    Given = [random.PickZipf(profile.GivenNames)],
                },
            ],
            Identifier =
            [
                new Identifier(random.Pick(profile.TokenSystems), $"P{id}"),
            ],
        };
    }
}
```

`src/Abm.Pyro.Performance/Generation/ObservationGenerator.cs`:

```csharp
using Hl7.Fhir.Model;

namespace Abm.Pyro.Performance.Generation;

internal static class ObservationGenerator
{
    public static Observation Create(string id, string patientId, DeterministicRandom random, DistributionProfile profile)
    {
        string code = random.PickZipf(profile.ObservationCodes);
        string system = random.PickZipf(profile.TokenSystems);
        DateTime effective = random.NextDateClusteredRecent(DistributionProfile.AnchorUtc, maxDaysBack: 3650);

        return new Observation
        {
            Id = id,
            Status = ObservationStatus.Final,
            Subject = new ResourceReference($"Patient/{patientId}"),
            Code = new CodeableConcept(system, code, $"Observation code {code}"),
            Effective = new FhirDateTime(new DateTimeOffset(effective, TimeSpan.Zero)),
            Value = new Quantity
            {
                Value = Math.Round((decimal)(random.NextDouble() * 200), 2),
                Unit = "mg/dL",
                System = "http://unitsofmeasure.org",
                Code = "mg/dL",
            },
        };
    }
}
```

`src/Abm.Pyro.Performance/Generation/EncounterGenerator.cs`:

```csharp
using Hl7.Fhir.Model;

namespace Abm.Pyro.Performance.Generation;

internal static class EncounterGenerator
{
    public static Encounter Create(string id, string patientId, string locationId, DeterministicRandom random, DistributionProfile profile)
    {
        DateTime start = random.NextDateClusteredRecent(DistributionProfile.AnchorUtc, maxDaysBack: 3650);

        return new Encounter
        {
            Id = id,
            Status = Encounter.EncounterStatus.Finished,
            Class = new Coding("http://terminology.hl7.org/CodeSystem/v3-ActCode", "AMB"),
            Subject = new ResourceReference($"Patient/{patientId}"),
            Period = new Period
            {
                StartElement = new FhirDateTime(new DateTimeOffset(start, TimeSpan.Zero)),
                EndElement = new FhirDateTime(new DateTimeOffset(start.AddHours(2), TimeSpan.Zero)),
            },
            Location =
            [
                new Encounter.LocationComponent { Location = new ResourceReference($"Location/{locationId}") },
            ],
        };
    }
}
```

`src/Abm.Pyro.Performance/Generation/DiagnosticReportGenerator.cs`:

```csharp
using Hl7.Fhir.Model;

namespace Abm.Pyro.Performance.Generation;

internal static class DiagnosticReportGenerator
{
    public static DiagnosticReport Create(string id, string patientId, string observationId, DeterministicRandom random, DistributionProfile profile)
    {
        string code = random.PickZipf(profile.ObservationCodes);
        DateTime issued = random.NextDateClusteredRecent(DistributionProfile.AnchorUtc, maxDaysBack: 3650);

        return new DiagnosticReport
        {
            Id = id,
            Status = DiagnosticReport.DiagnosticReportStatus.Final,
            Subject = new ResourceReference($"Patient/{patientId}"),
            Code = new CodeableConcept(random.PickZipf(profile.TokenSystems), code),
            IssuedElement = new Instant(new DateTimeOffset(issued, TimeSpan.Zero)),
            Result = [new ResourceReference($"Observation/{observationId}")],
        };
    }
}
```

`src/Abm.Pyro.Performance/Generation/LocationGenerator.cs`:

```csharp
using Hl7.Fhir.Model;

namespace Abm.Pyro.Performance.Generation;

internal static class LocationGenerator
{
    /// <summary>
    /// Geographic cluster centres, so 'near' searches have both dense metropolitan hits and
    /// isolated outliers to distinguish (spec §8.2). Latitude then longitude, as FHIR writes them.
    /// </summary>
    private static readonly (double Latitude, double Longitude)[] Clusters =
    [
        (-33.8688, 151.2093), // Sydney
        (-37.8136, 144.9631), // Melbourne
        (-27.4698, 153.0251), // Brisbane
        (-31.9523, 115.8613), // Perth
        (-42.8821, 147.3272), // Hobart
    ];

    public static Location Create(string id, DeterministicRandom random, DistributionProfile profile)
    {
        // One location in twenty is an outlier, placed well away from any cluster, so that a
        // bounded 'near' radius is genuinely selective rather than matching the whole table.
        bool isOutlier = random.Next(20) == 0;

        (double centreLatitude, double centreLongitude) = Clusters[random.Next(Clusters.Length)];
        double spread = isOutlier ? 8.0 : 0.25;

        double latitude = centreLatitude + ((random.NextDouble() - 0.5) * spread);
        double longitude = centreLongitude + ((random.NextDouble() - 0.5) * spread);

        return new Location
        {
            Id = id,
            Status = Location.LocationStatus.Active,
            Name = $"Location {id}",
            Position = new Location.PositionComponent
            {
                Latitude = (decimal)Math.Round(latitude, 6),
                Longitude = (decimal)Math.Round(longitude, 6),
            },
        };
    }
}
```

- [ ] **Step 4: Implement `CorpusGenerator`**

`src/Abm.Pyro.Performance/Generation/CorpusGenerator.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Abm.Pyro.Domain.Enums;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace Abm.Pyro.Performance.Generation;

/// <summary>
/// One generated resource, carrying the surrogate key it will be written under. Ids are assigned
/// here rather than by the database so the corpus is fully reproducible down to its keys, and so
/// index rows can be built in memory without a round trip (spec §8.1).
/// </summary>
public sealed record GeneratedResource(
    int ResourceStoreId,
    Resource Resource,
    FhirResourceTypeId ResourceType,
    DateTime LastUpdatedUtc);

/// <summary>
/// (seed, scale, profile) -> resources, as a pure function. No wall-clock time, no Guid.NewGuid,
/// no unordered iteration (spec §8.1).
/// </summary>
public static class CorpusGenerator
{
    /// <summary>
    /// The mix, as a proportion of the requested scale. Observations dominate because reference
    /// fan-out is what makes _has, chained and _include expensive, which is what needs measuring.
    /// </summary>
    private const double PatientShare = 0.10;
    private const double ObservationShare = 0.60;
    private const double EncounterShare = 0.15;
    private const double DiagnosticReportShare = 0.13;
    // Locations take the remainder, ~2%.

    public static IEnumerable<GeneratedResource> Generate(CorpusManifest manifest, DistributionProfile profile)
    {
        // This is an iterator method, so the guard below does not run until the caller starts
        // enumerating. That is acceptable here because every caller enumerates immediately, and
        // SeedCommand validates --scale itself before it reaches this point (Review Focus #1).
        ArgumentOutOfRangeException.ThrowIfLessThan(manifest.Scale, 1);

        var random = new DeterministicRandom(manifest.Seed);

        int patientCount = (int)(manifest.Scale * PatientShare);
        int observationCount = (int)(manifest.Scale * ObservationShare);
        int encounterCount = (int)(manifest.Scale * EncounterShare);
        int diagnosticReportCount = (int)(manifest.Scale * DiagnosticReportShare);
        int locationCount = manifest.Scale - patientCount - observationCount - encounterCount - diagnosticReportCount;

        int nextId = 1;

        // Order matters and is fixed: patients and locations first, so later types can reference
        // ids that already exist.
        var patientIds = new List<string>(patientCount);
        for (int i = 0; i < patientCount; i++)
        {
            string id = $"pat-{i:D8}";
            patientIds.Add(id);
            yield return new GeneratedResource(
                nextId++,
                PatientGenerator.Create(id, random, profile),
                FhirResourceTypeId.Patient,
                random.NextDateClusteredRecent(DistributionProfile.AnchorUtc, maxDaysBack: 1825));
        }

        var locationIds = new List<string>(locationCount);
        for (int i = 0; i < locationCount; i++)
        {
            string id = $"loc-{i:D8}";
            locationIds.Add(id);
            yield return new GeneratedResource(
                nextId++,
                LocationGenerator.Create(id, random, profile),
                FhirResourceTypeId.Location,
                random.NextDateClusteredRecent(DistributionProfile.AnchorUtc, maxDaysBack: 1825));
        }

        var observationIds = new List<string>(observationCount);
        for (int i = 0; i < observationCount; i++)
        {
            string id = $"obs-{i:D8}";
            observationIds.Add(id);
            yield return new GeneratedResource(
                nextId++,
                ObservationGenerator.Create(id, patientIds[i % patientIds.Count], random, profile),
                FhirResourceTypeId.Observation,
                random.NextDateClusteredRecent(DistributionProfile.AnchorUtc, maxDaysBack: 1825));
        }

        for (int i = 0; i < encounterCount; i++)
        {
            yield return new GeneratedResource(
                nextId++,
                EncounterGenerator.Create(
                    $"enc-{i:D8}",
                    patientIds[i % patientIds.Count],
                    locationIds[i % locationIds.Count],
                    random,
                    profile),
                FhirResourceTypeId.Encounter,
                random.NextDateClusteredRecent(DistributionProfile.AnchorUtc, maxDaysBack: 1825));
        }

        for (int i = 0; i < diagnosticReportCount; i++)
        {
            yield return new GeneratedResource(
                nextId++,
                DiagnosticReportGenerator.Create(
                    $"dr-{i:D8}",
                    patientIds[i % patientIds.Count],
                    observationIds[i % observationIds.Count],
                    random,
                    profile),
                FhirResourceTypeId.DiagnosticReport,
                random.NextDateClusteredRecent(DistributionProfile.AnchorUtc, maxDaysBack: 1825));
        }
    }

    /// <summary>
    /// A hash over the serialised corpus, used by the determinism tests and by 'seed --verify'.
    /// Streaming, because the 250k corpus must never be materialised in full just to hash it.
    /// </summary>
    public static string ComputeContentHash(IEnumerable<GeneratedResource> corpus)
    {
        var serializer = new FhirJsonSerializer();
        using var hasher = SHA256.Create();

        foreach (GeneratedResource generated in corpus)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(serializer.SerializeToString(generated.Resource));
            hasher.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        hasher.TransformFinalBlock([], 0, 0);
        return Convert.ToHexStringLower(hasher.Hash!);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter CorpusGeneratorTest`
Expected: PASS — ten tests (the two theory cases count individually).

If `Generate_ProducesAllFiveResourceTypes` fails at scale 1000, the location share has rounded to zero. Raise the test scale to 5000 rather than changing the mix, since the mix is what the 250k corpus needs.

- [ ] **Step 6: Commit**

```bash
git add src/Abm.Pyro.Performance/Generation src/Abm.Pyro.Performance.Test/Generation
git commit -m "feat: deterministic FHIR corpus generator across five resource types

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: The corpus host

The harness needs the **real** DI graph — `Program.cs` is 498 lines of inline registration with no extractable `AddPyroServices` method, so reproducing it would be a duplicate that rots. `WebApplicationFactory<Program>` is the only way to get the genuine graph, and `Program.cs` already ends with `public partial class Program { }` for exactly this purpose.

The container is **long-lived** (spec §9): seeded once into a named Docker volume and kept between runs, unlike `IntegrationTestFixture`, which disposes its container per run. `Abm.Pyro.Api.Test`'s fixture is not touched.

**Files:**
- Create: `src/Abm.Pyro.Performance/Hosting/PerformanceWebApplicationFactory.cs`
- Create: `src/Abm.Pyro.Performance/Hosting/TenantScope.cs`
- Create: `src/Abm.Pyro.Performance/Hosting/CorpusHost.cs`

**Interfaces:**
- Consumes: `CorpusManifest` (Task 1).
- Produces:
  - `CorpusHost.StartAsync(string containerName, int memoryLimitMegabytes)` → `Task<CorpusHost>`; properties `ConnectionString`, `Services` (`IServiceProvider`); methods `void StartHost()`, `Task ApplyMigrationsAsync()`, `Task<CorpusManifest?> ReadStoredManifestAsync()`, `Task WriteStoredManifestAsync(CorpusManifest)`, `Task RequireManifestAsync(CorpusManifest expected)`, `ValueTask DisposeAsync()`.
  - `TenantScope.Create(IServiceProvider services)` → `IServiceScope` with the scoped tenant already set.

- [ ] **Step 1: Implement `PerformanceWebApplicationFactory`**

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
/// Hosts the real Pyro DI graph against the harness's container. This mirrors
/// Abm.Pyro.Api.Test's PyroWebApplicationFactory deliberately: the harness must measure the SQL
/// the production pipeline emits, so it resolves production services from the production
/// registrations rather than building its own graph (spec §10).
/// </summary>
public sealed class PerformanceWebApplicationFactory(string sqlConnectionString)
    : WebApplicationFactory<Abm.Pyro.Api.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Production, not Development: EnableSensitiveDataLogging is switched on in Development
        // (Program.cs:362) and its overhead would show up in the elapsed measurements.
        builder.UseEnvironment("Production");

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
            // Migrations are applied by CorpusHost before the factory starts, so the version
            // check would race against the freshly migrated container. Removed for the same
            // reason Abm.Pyro.Api.Test removes it.
            ServiceDescriptor? versionCheck = services.FirstOrDefault(d =>
                d.ImplementationType ==
                typeof(AppStartupServiceManager<DatabaseVersionValidationOnStartupService>));

            if (versionCheck is not null)
            {
                services.Remove(versionCheck);
            }
        });
    }
}
```

**Note:** if `Abm.Pyro.Api.Program` is not reachable by that name, use the global namespace — `WebApplicationFactory<Program>` with a `using` alias — matching whatever `Abm.Pyro.Api.Test/Fixtures/PyroWebApplicationFactory.cs` does. That file is the reference implementation.

- [ ] **Step 2: Implement `TenantScope`**

`src/Abm.Pyro.Performance/Hosting/TenantScope.cs`:

```csharp
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Abm.Pyro.Performance.Hosting;

/// <summary>
/// PyroDbContext is built from ITenantService.GetScopedTenant() (Program.cs:355-366), which in an
/// HTTP request is resolved from the {tenant} route segment. The harness has no HTTP request, so
/// it must set the tenant on the scope by hand before resolving anything that touches the
/// database. This is the same thing ValidateAndPrimeResourceEndpointPoliciesOnStartupService does.
/// </summary>
public static class TenantScope
{
    public static IServiceScope Create(IServiceProvider services)
    {
        IServiceScope scope = services.CreateScope();

        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
        Tenant tenant = tenantService.GetTenantList().First();
        tenantService.SetScopedTenant(tenant);

        return scope;
    }
}
```

- [ ] **Step 3: Implement `CorpusHost`**

`src/Abm.Pyro.Performance/Hosting/CorpusHost.cs`:

```csharp
using System.Text.Json;
using Abm.Pyro.Performance.Generation;
using Abm.Pyro.Repository;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Abm.Pyro.Performance.Hosting;

/// <summary>
/// Owns the SQL Server container and the hosted Pyro services for the lifetime of a harness
/// command. The container is long-lived and reused across runs (spec §9) — reseeding a 250k
/// corpus takes minutes, so disposing it per run would make iteration impossible.
/// </summary>
public sealed class CorpusHost : IAsyncDisposable
{
    private readonly MsSqlContainer _container;
    private PerformanceWebApplicationFactory? _factory;

    private CorpusHost(MsSqlContainer container)
    {
        _container = container;
    }

    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>The real Pyro service provider. Resolve from it through TenantScope, never directly.</summary>
    public IServiceProvider Services =>
        _factory?.Services ?? throw new InvalidOperationException("StartHostAsync has not been called");

    /// <summary>
    /// Buffer pressure is a dial, not a data volume (spec §5.2). Capping container memory is how
    /// the harness makes the corpus not quite fit in the buffer pool, rather than growing the
    /// corpus until it outruns the machine's RAM.
    /// </summary>
    public static async Task<CorpusHost> StartAsync(string containerName, int memoryLimitMegabytes)
    {
        MsSqlContainer container = new MsSqlBuilder(image: "mcr.microsoft.com/mssql/server:2022-latest")
            .WithName(containerName)
            .WithReuse(true)
            .WithCreateParameterModifier(parameters =>
                parameters.HostConfig.Memory = memoryLimitMegabytes * 1024L * 1024L)
            .Build();

        // Spec §13: a container or migration failure must produce a clear message naming the
        // cause, not a raw Docker or socket exception the reader has to decode.
        try
        {
            await container.StartAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Could not start the SQL Server container '{containerName}'. Docker must be running, and the " +
                $"image mcr.microsoft.com/mssql/server:2022-latest must be pullable. Underlying cause: {exception.Message}",
                exception);
        }

        var host = new CorpusHost(container)
        {
            ConnectionString = container.GetConnectionString(),
        };

        return host;
    }

    public async Task ApplyMigrationsAsync()
    {
        var optionsBuilder = new DbContextOptionsBuilder<PyroDbContext>()
            .UseSqlServer(ConnectionString, o => o.UseNetTopologySuite());

        try
        {
            await using var context = new PyroDbContext(optionsBuilder.Options);
            await context.Database.MigrateAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Applying EF migrations to the harness container failed. If this names a pending model change, " +
                "the snapshot and the model have diverged and that must be fixed before anything is measured. " +
                $"Underlying cause: {exception.Message}",
                exception);
        }
    }

    /// <summary>
    /// Starts the hosted Pyro services. Must run after ApplyMigrationsAsync, because the startup
    /// services query tables the migrations create.
    /// </summary>
    public void StartHost()
    {
        _factory = new PerformanceWebApplicationFactory(ConnectionString);

        // Touching Services is what actually builds the host and runs the startup services.
        _ = _factory.Services;
    }

    /// <summary>
    /// The stored manifest lives in a table the harness owns, outside the EF model, so it can
    /// never affect a migration snapshot.
    /// </summary>
    public async Task WriteStoredManifestAsync(CorpusManifest manifest)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID('dbo.PerfCorpusManifest', 'U') IS NULL
                CREATE TABLE dbo.PerfCorpusManifest (ManifestJson NVARCHAR(MAX) NOT NULL);
            DELETE FROM dbo.PerfCorpusManifest;
            INSERT INTO dbo.PerfCorpusManifest (ManifestJson) VALUES (@json);
            """;
        command.Parameters.AddWithValue("@json", JsonSerializer.Serialize(manifest));
        await command.ExecuteNonQueryAsync();
    }

    public async Task<CorpusManifest?> ReadStoredManifestAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID('dbo.PerfCorpusManifest', 'U') IS NULL
                SELECT CAST(NULL AS NVARCHAR(MAX));
            ELSE
                SELECT TOP 1 ManifestJson FROM dbo.PerfCorpusManifest;
            """;

        object? result = await command.ExecuteScalarAsync();
        if (result is not string json)
        {
            return null;
        }

        return JsonSerializer.Deserialize<CorpusManifest>(json);
    }

    /// <summary>
    /// Refuse to run against a corpus that is not the one requested (spec §13). Measuring the
    /// wrong corpus silently is the single worst failure this harness could have.
    /// </summary>
    public async Task RequireManifestAsync(CorpusManifest expected)
    {
        CorpusManifest? stored = await ReadStoredManifestAsync();

        if (stored is null)
        {
            throw new InvalidOperationException(
                "No corpus has been seeded into this container. Run: " +
                $"dotnet run --project src/Abm.Pyro.Performance -- seed --scale {expected.Scale} --seed {expected.Seed} --profile {expected.Profile}");
        }

        if (!string.Equals(stored.ContentHash, expected.ContentHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Corpus hash mismatch. The container holds {stored.GeneratorVersion}/seed {stored.Seed}/scale {stored.Scale}/profile {stored.Profile} " +
                $"(hash {stored.ContentHash[..12]}), but {expected.ContentHash[..12]} was requested. Reseed with: " +
                $"dotnet run --project src/Abm.Pyro.Performance -- seed --scale {expected.Scale} --seed {expected.Seed} --profile {expected.Profile}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        // The container is deliberately NOT disposed: it is reused across runs (spec §9).
        // Remove it by hand with: docker rm -f <containerName>
        await Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Verify the host starts and resolves the real search service**

Create `src/Abm.Pyro.Performance.Test/Integration/CorpusHostTest.cs`:

```csharp
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Performance.Generation;
using Abm.Pyro.Performance.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Abm.Pyro.Performance.Test.Integration;

[Trait("Category", "Integration")]
public class CorpusHostTest
{
    [Fact]
    public async Task StartAsync_BringsUpAHostThatResolvesTheRealSearchService()
    {
        await using CorpusHost host = await CorpusHost.StartAsync("pyro-perf-test", memoryLimitMegabytes: 2048);
        await host.ApplyMigrationsAsync();
        host.StartHost();

        using IServiceScope scope = TenantScope.Create(host.Services);
        var search = scope.ServiceProvider.GetRequiredService<IResourceStoreSearch>();

        Assert.NotNull(search);
    }

    [Fact]
    public async Task RequireManifestAsync_RefusesWhenNothingHasBeenSeeded()
    {
        await using CorpusHost host = await CorpusHost.StartAsync("pyro-perf-test", memoryLimitMegabytes: 2048);
        await host.ApplyMigrationsAsync();

        var expected = new CorpusManifest(CorpusManifest.CurrentGeneratorVersion, 42, 1000, "default");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.RequireManifestAsync(expected));

        Assert.Contains("No corpus has been seeded", exception.Message);
    }

    [Fact]
    public async Task RequireManifestAsync_RefusesWhenTheStoredCorpusIsADifferentOne()
    {
        await using CorpusHost host = await CorpusHost.StartAsync("pyro-perf-test", memoryLimitMegabytes: 2048);
        await host.ApplyMigrationsAsync();
        await host.WriteStoredManifestAsync(
            new CorpusManifest(CorpusManifest.CurrentGeneratorVersion, 42, 1000, "default"));

        var different = new CorpusManifest(CorpusManifest.CurrentGeneratorVersion, 42, 250000, "default");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.RequireManifestAsync(different));

        Assert.Contains("Corpus hash mismatch", exception.Message);
    }
}
```

- [ ] **Step 5: Run the host tests**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter CorpusHostTest`
Expected: PASS — three tests. Docker must be running.

If `StartAsync` fails with a container-name conflict from a previous run, that is the reuse path working incorrectly; run `docker rm -f pyro-perf-test` and retry.

- [ ] **Step 6: Commit**

```bash
git add src/Abm.Pyro.Performance/Hosting src/Abm.Pyro.Performance.Test/Integration
git commit -m "feat: long-lived corpus host over the real Pyro DI graph

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 5: Bulk index writer and the seed command

This is the seam **sub-project C (re-index) will reuse** (spec §7): "run the real setters over a resource and write index rows in bulk" *is* re-indexing. The boundary is explicit here so C generalises it rather than reimplementing it.

Two production realities shape the implementation:

- **`SqlBulkCopy` bypasses EF value converters**, so `ResourceStore.Json` must be compressed by hand with `ResourceStoreJsonCompressionConversion.Zip`, which is already `public static`.
- **`SqlBulkCopy` cannot bind a NetTopologySuite `Point` to a `geography` column** without `Microsoft.SqlServer.Types`, which carries native dependencies. `IndexPosition` is therefore written through EF in batches instead. It is by far the smallest index table — only `Location.position` writes to it, and Locations are ~2% of the corpus — so the cost is seconds, not minutes.

**Files:**
- Create: `src/Abm.Pyro.Performance/Seeding/BulkIndexWriter.cs`
- Create: `src/Abm.Pyro.Performance/Seeding/TableSizeReporter.cs`
- Create: `src/Abm.Pyro.Performance/Cli/SeedCommand.cs`
- Modify: `src/Abm.Pyro.Performance/Program.cs`
- Create: `src/Abm.Pyro.Performance.Test/Integration/SmokeSeedTest.cs`

**Interfaces:**
- Consumes: `CorpusHost`, `TenantScope` (Task 4); `GeneratedResource`, `CorpusGenerator`, `CorpusManifest`, `DistributionProfile` (Tasks 1–3); and from production, `IIndexer.Process(Resource, FhirResourceTypeId)` → `IndexerOutcome`.
- Produces:
  - `BulkIndexWriter(string connectionString, IServiceProvider services)` with `Task<SeedOutcome> WriteAsync(IEnumerable<GeneratedResource> corpus, int batchSize, CancellationToken)`, where `record SeedOutcome(int ResourceCount, IReadOnlyDictionary<string, int> IndexRowCountByTable, TimeSpan Elapsed)`.
  - `BulkIndexWriter.AssertDatabaseIsEmptyAsync()` — the Review Focus #2 guard.
  - `TableSizeReporter.ReportAsync(string connectionString)` → `IReadOnlyList<TableSize>`, `record TableSize(string TableName, long RowCount, long ReservedKb, long DataKb, long IndexKb)`.

- [ ] **Step 1: Write the failing smoke test**

`src/Abm.Pyro.Performance.Test/Integration/SmokeSeedTest.cs`:

```csharp
using Abm.Pyro.Performance.Generation;
using Abm.Pyro.Performance.Hosting;
using Abm.Pyro.Performance.Seeding;

namespace Abm.Pyro.Performance.Test.Integration;

[Trait("Category", "Integration")]
public class SmokeSeedTest
{
    [Fact]
    public async Task WriteAsync_SeedsResourcesAndIndexRows()
    {
        await using CorpusHost host = await CorpusHost.StartAsync("pyro-perf-smoke", memoryLimitMegabytes: 2048);
        await host.ApplyMigrationsAsync();
        host.StartHost();

        var manifest = new CorpusManifest(CorpusManifest.CurrentGeneratorVersion, Seed: 42, Scale: 1000, Profile: "default");
        var writer = new BulkIndexWriter(host.ConnectionString, host.Services);

        SeedOutcome outcome = await writer.WriteAsync(
            CorpusGenerator.Generate(manifest, DistributionProfile.Default()),
            batchSize: 500,
            CancellationToken.None);

        Assert.Equal(1000, outcome.ResourceCount);
        Assert.True(outcome.IndexRowCountByTable["IndexString"] > 0, "patients must produce name index rows");
        Assert.True(outcome.IndexRowCountByTable["IndexToken"] > 0, "observations must produce code index rows");
        Assert.True(outcome.IndexRowCountByTable["IndexReference"] > 0, "observations must produce subject index rows");
        Assert.True(outcome.IndexRowCountByTable["IndexPosition"] > 0, "locations must produce position index rows");
    }

    [Fact]
    public async Task AssertDatabaseIsEmptyAsync_RefusesToSeedOverAnExistingCorpus()
    {
        // Review Focus #2: a second seed over a populated ResourceStore would fail mid-write on a
        // primary-key violation, leaving a half-seeded corpus whose manifest still claims validity.
        await using CorpusHost host = await CorpusHost.StartAsync("pyro-perf-smoke", memoryLimitMegabytes: 2048);
        await host.ApplyMigrationsAsync();
        host.StartHost();

        var manifest = new CorpusManifest(CorpusManifest.CurrentGeneratorVersion, Seed: 42, Scale: 100, Profile: "default");
        var writer = new BulkIndexWriter(host.ConnectionString, host.Services);

        await writer.WriteAsync(
            CorpusGenerator.Generate(manifest, DistributionProfile.Default()),
            batchSize: 100,
            CancellationToken.None);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.AssertDatabaseIsEmptyAsync());

        Assert.Contains("already holds", exception.Message);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter SmokeSeedTest`
Expected: FAIL — `BulkIndexWriter` does not exist.

- [ ] **Step 3: Implement `BulkIndexWriter`**

`src/Abm.Pyro.Performance/Seeding/BulkIndexWriter.cs`:

```csharp
using System.Data;
using System.Diagnostics;
using Abm.Pyro.Application.Indexing;
using Abm.Pyro.Domain.Indexing;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Performance.Generation;
using Abm.Pyro.Performance.Hosting;
using Abm.Pyro.Repository;
using Abm.Pyro.Repository.Conversion;
using Hl7.Fhir.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Seeding;

public sealed record SeedOutcome(
    int ResourceCount,
    IReadOnlyDictionary<string, int> IndexRowCountByTable,
    TimeSpan Elapsed);

/// <summary>
/// Runs the real indexer over generated resources and writes the results in bulk.
///
/// This is deliberately the seam sub-project C (re-index) will reuse (spec §7): "run the real
/// setters over a resource and write index rows in bulk" is exactly what re-indexing does. C should
/// generalise this class — swapping the generated corpus for resources read out of ResourceStore —
/// rather than reimplementing it.
///
/// Indexing semantics are never reimplemented here. IIndexer is the production composition of all
/// eight setters (Abm.Pyro.Application/Indexing/Indexer.cs), and it is what runs.
/// </summary>
public sealed class BulkIndexWriter(string connectionString, IServiceProvider services)
{
    private static readonly string[] IndexTableNames =
    [
        "IndexString", "IndexToken", "IndexReference", "IndexDateTime",
        "IndexQuantity", "IndexUri", "IndexPosition",
    ];

    /// <summary>
    /// Review Focus #2. SqlBulkCopy with KeepIdentity into a populated ResourceStore fails
    /// mid-write on a primary-key violation, leaving a half-seeded corpus. Refuse before the
    /// first write instead.
    /// </summary>
    public async Task AssertDatabaseIsEmptyAsync()
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM dbo.ResourceStore";

        int count = Convert.ToInt32(await command.ExecuteScalarAsync());
        if (count > 0)
        {
            throw new InvalidOperationException(
                $"The database already holds {count} ResourceStore rows. Seeding over an existing corpus " +
                "would fail part-way through and leave the corpus in an unmeasurable state. Drop the " +
                "container first: docker rm -f <containerName>");
        }
    }

    public async Task<SeedOutcome> WriteAsync(
        IEnumerable<GeneratedResource> corpus,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var serializer = new FhirJsonSerializer();
        var indexRowCounts = IndexTableNames.ToDictionary(name => name, _ => 0, StringComparer.Ordinal);
        int resourceCount = 0;

        var resourceBatch = new List<GeneratedResource>(batchSize);
        var outcomeBatch = new List<(int ResourceStoreId, IndexerOutcome Outcome)>(batchSize);

        using IServiceScope scope = TenantScope.Create(services);
        var indexer = scope.ServiceProvider.GetRequiredService<IIndexer>();

        foreach (GeneratedResource generated in corpus)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IndexerOutcome outcome = await indexer.Process(generated.Resource, generated.ResourceType);

            resourceBatch.Add(generated);
            outcomeBatch.Add((generated.ResourceStoreId, outcome));

            if (resourceBatch.Count >= batchSize)
            {
                await FlushAsync(resourceBatch, outcomeBatch, serializer, indexRowCounts, cancellationToken);
                resourceCount += resourceBatch.Count;
                resourceBatch.Clear();
                outcomeBatch.Clear();
            }
        }

        if (resourceBatch.Count > 0)
        {
            await FlushAsync(resourceBatch, outcomeBatch, serializer, indexRowCounts, cancellationToken);
            resourceCount += resourceBatch.Count;
        }

        stopwatch.Stop();
        return new SeedOutcome(resourceCount, indexRowCounts, stopwatch.Elapsed);
    }

    private async Task FlushAsync(
        List<GeneratedResource> resourceBatch,
        List<(int ResourceStoreId, IndexerOutcome Outcome)> outcomeBatch,
        FhirJsonSerializer serializer,
        Dictionary<string, int> indexRowCounts,
        CancellationToken cancellationToken)
    {
        await WriteResourceStoreAsync(resourceBatch, serializer, cancellationToken);

        indexRowCounts["IndexString"] += await WriteStringIndexAsync(outcomeBatch, cancellationToken);
        indexRowCounts["IndexToken"] += await WriteTokenIndexAsync(outcomeBatch, cancellationToken);
        indexRowCounts["IndexReference"] += await WriteReferenceIndexAsync(outcomeBatch, cancellationToken);
        indexRowCounts["IndexDateTime"] += await WriteDateTimeIndexAsync(outcomeBatch, cancellationToken);
        indexRowCounts["IndexQuantity"] += await WriteQuantityIndexAsync(outcomeBatch, cancellationToken);
        indexRowCounts["IndexUri"] += await WriteUriIndexAsync(outcomeBatch, cancellationToken);
        indexRowCounts["IndexPosition"] += await WritePositionIndexViaEfAsync(outcomeBatch, cancellationToken);
    }

    private async Task WriteResourceStoreAsync(
        List<GeneratedResource> batch,
        FhirJsonSerializer serializer,
        CancellationToken cancellationToken)
    {
        var table = new DataTable();
        table.Columns.Add("ResourceStoreId", typeof(int));
        table.Columns.Add("ResourceId", typeof(string));
        table.Columns.Add("VersionId", typeof(int));
        table.Columns.Add("ResourceType", typeof(int));
        table.Columns.Add("IsCurrent", typeof(bool));
        table.Columns.Add("IsDeleted", typeof(bool));
        table.Columns.Add("HttpVerb", typeof(int));
        table.Columns.Add("Json", typeof(byte[]));
        table.Columns.Add("LastUpdatedUtc", typeof(DateTime));
        table.Columns.Add("RowVersion", typeof(int));

        foreach (GeneratedResource generated in batch)
        {
            // SqlBulkCopy bypasses EF value converters, so the GZip compression that
            // ResourceStoreJsonCompressionConversion normally applies is done by hand here.
            byte[] compressed = ResourceStoreJsonCompressionConversion.Zip(
                serializer.SerializeToString(generated.Resource));

            table.Rows.Add(
                generated.ResourceStoreId,
                generated.Resource.Id,
                1,
                (int)generated.ResourceType,
                true,
                false,
                (int)Abm.Pyro.Domain.Enums.HttpVerbId.Post,
                compressed,
                generated.LastUpdatedUtc,
                1);
        }

        await BulkCopyAsync("ResourceStore", table, cancellationToken);
    }

    private Task<int> WriteStringIndexAsync(
        List<(int ResourceStoreId, IndexerOutcome Outcome)> batch,
        CancellationToken cancellationToken)
    {
        var table = NewIndexTable();
        table.Columns.Add("Value", typeof(string));

        foreach ((int resourceStoreId, IndexerOutcome outcome) in batch)
        {
            foreach (IndexString row in outcome.StringIndexList)
            {
                table.Rows.Add(resourceStoreId, row.SearchParameterStoreId, row.Value);
            }
        }

        return BulkCopyCountingAsync("IndexString", table, cancellationToken);
    }

    private Task<int> WriteTokenIndexAsync(
        List<(int ResourceStoreId, IndexerOutcome Outcome)> batch,
        CancellationToken cancellationToken)
    {
        var table = NewIndexTable();
        table.Columns.Add("Code", typeof(string));
        table.Columns.Add("System", typeof(string));

        foreach ((int resourceStoreId, IndexerOutcome outcome) in batch)
        {
            foreach (IndexToken row in outcome.TokenIndexList)
            {
                table.Rows.Add(resourceStoreId, row.SearchParameterStoreId, (object?)row.Code ?? DBNull.Value, (object?)row.System ?? DBNull.Value);
            }
        }

        return BulkCopyCountingAsync("IndexToken", table, cancellationToken);
    }

    private Task<int> WriteReferenceIndexAsync(
        List<(int ResourceStoreId, IndexerOutcome Outcome)> batch,
        CancellationToken cancellationToken)
    {
        var table = NewIndexTable();
        table.Columns.Add("ServiceBaseUrlId", typeof(int));
        table.Columns.Add("ResourceType", typeof(int));
        table.Columns.Add("ResourceId", typeof(string));
        table.Columns.Add("VersionId", typeof(string));
        table.Columns.Add("CanonicalVersionId", typeof(string));

        foreach ((int resourceStoreId, IndexerOutcome outcome) in batch)
        {
            foreach (IndexReference row in outcome.ReferenceIndexList)
            {
                table.Rows.Add(
                    resourceStoreId,
                    row.SearchParameterStoreId,
                    (object?)row.ServiceBaseUrlId ?? DBNull.Value,
                    (int)row.ResourceType,
                    row.ResourceId,
                    (object?)row.VersionId ?? DBNull.Value,
                    // Written on every ingest and read by nothing (spec §2.2f). It is written here
                    // anyway, because the corpus must reflect today's ingest cost, not a hoped-for
                    // one — sub-project D's case for dropping it rests on measuring it first.
                    (object?)row.CanonicalVersionId ?? DBNull.Value);
            }
        }

        return BulkCopyCountingAsync("IndexReference", table, cancellationToken);
    }

    private Task<int> WriteUriIndexAsync(
        List<(int ResourceStoreId, IndexerOutcome Outcome)> batch,
        CancellationToken cancellationToken)
    {
        var table = NewIndexTable();
        table.Columns.Add("Uri", typeof(string));

        foreach ((int resourceStoreId, IndexerOutcome outcome) in batch)
        {
            foreach (IndexUri row in outcome.UriIndexList)
            {
                table.Rows.Add(resourceStoreId, row.SearchParameterStoreId, row.Uri);
            }
        }

        return BulkCopyCountingAsync("IndexUri", table, cancellationToken);
    }

    // WriteDateTimeIndexAsync and WriteQuantityIndexAsync follow the identical shape:
    // NewIndexTable(), add that table's own value columns, copy every property the corresponding
    // Abm.Pyro.Domain/Model/IndexDateTime.cs and IndexQuantity.cs declares, then
    // BulkCopyCountingAsync. Read both model classes and mirror their properties exactly —
    // including IndexQuantity's CodeHigh, SystemHigh and UnitHigh, which spec §2.2f notes are
    // written and never read, for the same reason CanonicalVersionId is written above.

    /// <summary>
    /// IndexPosition goes through EF rather than SqlBulkCopy: SqlBulkCopy cannot bind a
    /// NetTopologySuite Point to a geography column without Microsoft.SqlServer.Types and its
    /// native dependencies. Only Location.position writes this table, so it is by far the
    /// smallest of the seven and the EF cost is seconds, not minutes.
    /// </summary>
    private async Task<int> WritePositionIndexViaEfAsync(
        List<(int ResourceStoreId, IndexerOutcome Outcome)> batch,
        CancellationToken cancellationToken)
    {
        var rows = new List<IndexPosition>();
        foreach ((int resourceStoreId, IndexerOutcome outcome) in batch)
        {
            foreach (IndexPosition row in outcome.PositionIndexList)
            {
                rows.Add(new IndexPosition(
                    indexPositionId: null,
                    resourceStoreId: resourceStoreId,
                    resourceStore: null,
                    searchParameterStoreId: row.SearchParameterStoreId,
                    searchParameterStore: null,
                    position: row.Position));
            }
        }

        if (rows.Count == 0)
        {
            return 0;
        }

        var optionsBuilder = new DbContextOptionsBuilder<PyroDbContext>()
            .UseSqlServer(connectionString, o => o.UseNetTopologySuite());

        await using var context = new PyroDbContext(optionsBuilder.Options);
        context.Set<IndexPosition>().AddRange(rows);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        return rows.Count;
    }

    private static DataTable NewIndexTable()
    {
        var table = new DataTable();
        table.Columns.Add("ResourceStoreId", typeof(int));
        table.Columns.Add("SearchParameterStoreId", typeof(int));
        return table;
    }

    private async Task<int> BulkCopyCountingAsync(string tableName, DataTable table, CancellationToken cancellationToken)
    {
        if (table.Rows.Count == 0)
        {
            return 0;
        }

        await BulkCopyAsync(tableName, table, cancellationToken);
        return table.Rows.Count;
    }

    private async Task BulkCopyAsync(string tableName, DataTable table, CancellationToken cancellationToken)
    {
        if (table.Rows.Count == 0)
        {
            return;
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // KeepIdentity, because the generator assigns ResourceStoreId itself so that index rows
        // can be built in memory without a round trip, and so the corpus is reproducible down to
        // its surrogate keys.
        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, externalTransaction: null)
        {
            DestinationTableName = $"dbo.{tableName}",
            BatchSize = 5000,
            BulkCopyTimeout = 300,
        };

        foreach (DataColumn column in table.Columns)
        {
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulkCopy.WriteToServerAsync(table, cancellationToken);
    }
}
```

**Note for the implementer:** the two elided `WriteXxxIndexAsync` methods are not optional. Write them by reading `Abm.Pyro.Domain/Model/IndexDateTime.cs` and `IndexQuantity.cs` and mirroring every declared property as a `DataTable` column of the matching CLR type, exactly as `WriteReferenceIndexAsync` above does. Nullable reference and value columns must use `(object?)value ?? DBNull.Value`; enum columns cast to `int`.

- [ ] **Step 4: Implement `TableSizeReporter` — the spec's first deliverable (§11)**

`src/Abm.Pyro.Performance/Seeding/TableSizeReporter.cs`:

```csharp
using Microsoft.Data.SqlClient;

namespace Abm.Pyro.Performance.Seeding;

public sealed record TableSize(string TableName, long RowCount, long ReservedKb, long DataKb, long IndexKb);

/// <summary>
/// Spec §11: the harness's first deliverable is replacing §5.1's estimated per-table sizes with
/// measured fact, BEFORE any index design is committed to.
/// </summary>
public static class TableSizeReporter
{
    private static readonly string[] Tables =
    [
        "ResourceStore", "IndexString", "IndexToken", "IndexReference",
        "IndexDateTime", "IndexQuantity", "IndexUri", "IndexPosition",
    ];

    public static async Task<IReadOnlyList<TableSize>> ReportAsync(string connectionString)
    {
        var results = new List<TableSize>(Tables.Length);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (string table in Tables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    SUM(p.rows) AS [RowCount],
                    SUM(a.total_pages) * 8 AS ReservedKb,
                    SUM(a.data_pages) * 8 AS DataKb,
                    (SUM(a.used_pages) - SUM(a.data_pages)) * 8 AS IndexKb
                FROM sys.tables t
                INNER JOIN sys.indexes i ON t.object_id = i.object_id
                INNER JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
                INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
                WHERE t.name = @tableName AND i.index_id <= 1
                """;
            command.Parameters.AddWithValue("@tableName", table);

            await using SqlDataReader reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync() && !reader.IsDBNull(0))
            {
                results.Add(new TableSize(
                    table,
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    reader.GetInt64(2),
                    reader.GetInt64(3)));
            }
            else
            {
                results.Add(new TableSize(table, 0, 0, 0, 0));
            }
        }

        return results;
    }
}
```

- [ ] **Step 5: Implement the `seed` command**

`src/Abm.Pyro.Performance/Cli/SeedCommand.cs`:

```csharp
using System.CommandLine;
using Abm.Pyro.Performance.Generation;
using Abm.Pyro.Performance.Hosting;
using Abm.Pyro.Performance.Seeding;

namespace Abm.Pyro.Performance.Cli;

public static class SeedCommand
{
    public const string DefaultContainerName = "pyro-perf-corpus";

    public static Command Create()
    {
        var scaleOption = new Option<int>("--scale", () => 250_000, "Number of resources to generate");
        var seedOption = new Option<int>("--seed", () => 42, "RNG seed");
        var profileOption = new Option<string>("--profile", () => "default", "Distribution profile name");
        var reportOption = new Option<bool>("--report", () => false, "Print measured per-table sizes after seeding");
        var memoryOption = new Option<int>("--memory-mb", () => 4096, "SQL container memory cap, the buffer-pressure dial");
        var containerOption = new Option<string>("--container", () => DefaultContainerName, "Docker container name");

        var command = new Command("seed", "Generate and bulk-load a corpus")
        {
            scaleOption, seedOption, profileOption, reportOption, memoryOption, containerOption,
        };

        command.SetHandler(async (scale, seed, profile, report, memoryMb, container) =>
        {
            // Review Focus #1: refuse a nonsensical scale before touching Docker at all.
            if (scale < 1)
            {
                Console.Error.WriteLine($"--scale must be at least 1; got {scale}.");
                Environment.ExitCode = 1;
                return;
            }

            if (!string.Equals(profile, "default", StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"Unknown profile '{profile}'. The only profile defined is 'default'.");
                Environment.ExitCode = 1;
                return;
            }

            var manifest = new CorpusManifest(CorpusManifest.CurrentGeneratorVersion, seed, scale, profile);

            await using CorpusHost host = await CorpusHost.StartAsync(container, memoryMb);
            await host.ApplyMigrationsAsync();
            host.StartHost();

            var writer = new BulkIndexWriter(host.ConnectionString, host.Services);
            await writer.AssertDatabaseIsEmptyAsync();

            Console.WriteLine($"Seeding {scale:N0} resources (seed {seed}, profile {profile}, hash {manifest.ContentHash[..12]})...");

            SeedOutcome outcome = await writer.WriteAsync(
                CorpusGenerator.Generate(manifest, DistributionProfile.Default()),
                batchSize: 2000,
                CancellationToken.None);

            await host.WriteStoredManifestAsync(manifest);

            Console.WriteLine($"Seeded {outcome.ResourceCount:N0} resources in {outcome.Elapsed.TotalSeconds:N1}s.");
            foreach ((string table, int count) in outcome.IndexRowCountByTable.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                Console.WriteLine($"  {table,-18} {count,12:N0} rows");
            }

            if (report)
            {
                Console.WriteLine();
                Console.WriteLine("Measured table sizes (spec §11 — these replace §5.1's estimates):");
                Console.WriteLine($"  {"Table",-18} {"Rows",12} {"Reserved MB",12} {"Data MB",10} {"Index MB",10}");

                foreach (TableSize size in await TableSizeReporter.ReportAsync(host.ConnectionString))
                {
                    Console.WriteLine(
                        $"  {size.TableName,-18} {size.RowCount,12:N0} {size.ReservedKb / 1024.0,12:N1} " +
                        $"{size.DataKb / 1024.0,10:N1} {size.IndexKb / 1024.0,10:N1}");
                }
            }
        }, scaleOption, seedOption, profileOption, reportOption, memoryOption, containerOption);

        return command;
    }
}
```

Then wire it up in `src/Abm.Pyro.Performance/Program.cs`, replacing the placeholder comment:

```csharp
        root.AddCommand(Cli.SeedCommand.Create());
```

- [ ] **Step 6: Run the smoke tests to verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter SmokeSeedTest`
Expected: PASS — two tests.

- [ ] **Step 7: Take the first measured sizes — the spec's first deliverable**

Run: `dotnet run --project src/Abm.Pyro.Performance -- seed --scale 250000 --report`
Expected: completes in roughly 3 minutes (spec §16.1) and prints measured per-table sizes.

Record the printed table in a new `assets/perf/measured-sizes.md`, with a one-line note saying it supersedes spec §5.1's estimates and giving the date and scale. This is the evidence every later index decision rests on.

- [ ] **Step 8: Commit**

```bash
mkdir -p assets/perf
git add src/Abm.Pyro.Performance src/Abm.Pyro.Performance.Test assets/perf/measured-sizes.md
git commit -m "feat: bulk index writer and seed command with measured table sizes

Runs the real IIndexer over generated resources and bulk-loads the result.
This is the seam sub-project C will generalise for re-indexing.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 6: SQL capture and the query runner

The harness measures the SQL **Pyro actually emits**, never hand-written SQL (spec §10). `QueryRunner` drives the production pipeline — `ISearchQueryService.Process(resourceType, queryString)` → `IResourceStoreSearch.GetSearch(outcome)` — and an EF `DbCommandInterceptor` captures every command on the way out.

Because `GetSearch` runs `CountAsync()` at `ResourceStoreSearch.cs:56` and then the paged query at `:67`, a single search captures **two** commands. That double execution is exactly what sub-project E exists to remove (spec §2.2c), so capturing both separately is a feature, not noise.

**Files:**
- Create: `src/Abm.Pyro.Performance/Measurement/CapturedCommand.cs`
- Create: `src/Abm.Pyro.Performance/Measurement/SqlCaptureInterceptor.cs`
- Create: `src/Abm.Pyro.Performance/Measurement/QueryRunner.cs`
- Create: `src/Abm.Pyro.Performance.Test/Integration/QueryRunnerTest.cs`

**Interfaces:**
- Consumes: `CorpusHost`, `TenantScope` (Task 4); production `ISearchQueryService`, `IResourceStoreSearch`, `SearchQueryServiceOutcome`, `ResourceStoreSearchOutcome`.
- Produces:
  - `record CapturedCommand(string CommandText, IReadOnlyDictionary<string, object?> Parameters, TimeSpan Elapsed)`.
  - `SqlCaptureInterceptor` with `IReadOnlyList<CapturedCommand> Captured` and `void Reset()`.
  - `QueryRunner(CorpusHost host)` with `Task<QueryExecution> ExecuteAsync(string resourceTypeName, string queryString)`, where `record QueryExecution(int ResultCardinality, int TotalCount, IReadOnlyList<CapturedCommand> Commands)`.

- [ ] **Step 1: Write the failing query-runner test**

`src/Abm.Pyro.Performance.Test/Integration/QueryRunnerTest.cs`:

```csharp
using Abm.Pyro.Performance.Generation;
using Abm.Pyro.Performance.Hosting;
using Abm.Pyro.Performance.Measurement;
using Abm.Pyro.Performance.Seeding;

namespace Abm.Pyro.Performance.Test.Integration;

[Trait("Category", "Integration")]
public class QueryRunnerTest : IAsyncLifetime
{
    private CorpusHost _host = default!;

    public async Task InitializeAsync()
    {
        _host = await CorpusHost.StartAsync("pyro-perf-runner", memoryLimitMegabytes: 2048);
        await _host.ApplyMigrationsAsync();
        _host.StartHost();

        var manifest = new CorpusManifest(CorpusManifest.CurrentGeneratorVersion, 42, 2000, "default");
        var writer = new BulkIndexWriter(_host.ConnectionString, _host.Services);

        // Seeding is skipped when the reused container already holds this corpus.
        if (await _host.ReadStoredManifestAsync() is null)
        {
            await writer.WriteAsync(
                CorpusGenerator.Generate(manifest, DistributionProfile.Default()),
                batchSize: 500,
                CancellationToken.None);
            await _host.WriteStoredManifestAsync(manifest);
        }
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task ExecuteAsync_CapturesTheSqlTheRealPipelineEmits()
    {
        var runner = new QueryRunner(_host);

        QueryExecution execution = await runner.ExecuteAsync("Patient", "family=Smith");

        Assert.NotEmpty(execution.Commands);
        Assert.All(execution.Commands, command => Assert.Contains("ResourceStore", command.CommandText));
    }

    [Fact]
    public async Task ExecuteAsync_CapturesBothTheCountAndThePagedQuery()
    {
        // ResourceStoreSearch.GetSearch runs CountAsync at :56 and the paged query at :67 —
        // the full predicate twice. Spec §2.2c calls this plausibly the single largest available
        // win, and sub-project E removes it. Capturing both is how that claim gets evidence.
        var runner = new QueryRunner(_host);

        QueryExecution execution = await runner.ExecuteAsync("Patient", "family=Smith");

        Assert.True(execution.Commands.Count >= 2,
            "a search that matches rows must emit a COUNT and a paged SELECT");
        Assert.Contains(execution.Commands, command => command.CommandText.Contains("COUNT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_ReportsResultCardinality()
    {
        // Spec §10.1: cardinality is a first-class field, not a footnote. Without it a changed
        // result set reads as a performance delta and the programme draws the wrong conclusion.
        var runner = new QueryRunner(_host);

        QueryExecution execution = await runner.ExecuteAsync("Patient", "family=Smith");

        Assert.True(execution.TotalCount > 0);
    }

    [Fact]
    public async Task ExecuteAsync_FailsLoudlyOnAMisspelledSearchParameter()
    {
        // Review Focus #3: an unsupported parameter makes SearchQueryServiceOutcome.HasInvalidQuery
        // true and the search returns everything, which reads as a spectacular performance result.
        var runner = new QueryRunner(_host);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.ExecuteAsync("Patient", "familyy=Smith"));

        Assert.Contains("familyy", exception.Message);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter QueryRunnerTest`
Expected: FAIL — `QueryRunner` does not exist.

- [ ] **Step 3: Implement the capture interceptor**

`src/Abm.Pyro.Performance/Measurement/CapturedCommand.cs`:

```csharp
namespace Abm.Pyro.Performance.Measurement;

/// <summary>One SQL statement the production pipeline emitted, with its bound parameters.</summary>
public sealed record CapturedCommand(
    string CommandText,
    IReadOnlyDictionary<string, object?> Parameters,
    TimeSpan Elapsed);
```

`src/Abm.Pyro.Performance/Measurement/SqlCaptureInterceptor.cs`:

```csharp
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Abm.Pyro.Performance.Measurement;

/// <summary>
/// Captures the SQL EF actually produces. The spec is emphatic that the pipeline-driven
/// measurement is the source of truth and hand-written SQL is only ever a hypothesis generator
/// (spec §10), and this interceptor is what makes that possible.
/// </summary>
public sealed class SqlCaptureInterceptor : DbCommandInterceptor
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

    public void Reset()
    {
        lock (_gate)
        {
            _captured.Clear();
        }
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Record(command, eventData.Duration);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        Record(command, eventData.Duration);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        Record(command, eventData.Duration);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        Record(command, eventData.Duration);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    private void Record(DbCommand command, TimeSpan duration)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (DbParameter parameter in command.Parameters)
        {
            parameters[parameter.ParameterName] = parameter.Value;
        }

        lock (_gate)
        {
            _captured.Add(new CapturedCommand(command.CommandText, parameters, duration));
        }
    }
}
```

- [ ] **Step 4: Implement `QueryRunner`**

`src/Abm.Pyro.Performance/Measurement/QueryRunner.cs`:

```csharp
using Abm.Pyro.Application.SearchQuery;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Performance.Hosting;
using Abm.Pyro.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Measurement;

public sealed record QueryExecution(
    int ResultCardinality,
    int TotalCount,
    IReadOnlyList<CapturedCommand> Commands);

/// <summary>
/// Drives a FHIR query string through the real search pipeline and captures the SQL it emits.
/// Nothing here reimplements query parsing or predicate construction — ISearchQueryService and
/// IResourceStoreSearch are the production types, resolved from the production DI graph.
/// </summary>
public sealed class QueryRunner(CorpusHost host)
{
    public async Task<QueryExecution> ExecuteAsync(string resourceTypeName, string queryString)
    {
        if (!Enum.TryParse(resourceTypeName, ignoreCase: false, out FhirResourceTypeId resourceType))
        {
            throw new InvalidOperationException($"'{resourceTypeName}' is not a FhirResourceTypeId value.");
        }

        var interceptor = new SqlCaptureInterceptor();

        using IServiceScope scope = TenantScope.Create(host.Services);

        // The interceptor is attached to the scope's own context instance, so nothing outside
        // this execution is captured.
        var context = scope.ServiceProvider.GetRequiredService<PyroDbContext>();
        context.Database.AddInterceptors(interceptor);

        var searchQueryService = scope.ServiceProvider.GetRequiredService<ISearchQueryService>();
        SearchQueryServiceOutcome searchOutcome = await searchQueryService.Process(resourceType, queryString);

        // Review Focus #3. An invalid or unsupported parameter is silently dropped from the
        // predicate, and the search then returns the whole resource type — which would be
        // recorded as a startlingly fast query rather than as the mistake it is.
        if (searchOutcome.HasInvalidQuery)
        {
            throw new InvalidOperationException(
                $"Query '{resourceTypeName}?{queryString}' contains invalid parameters and would not be measured " +
                $"as written: {string.Join("; ", searchOutcome.InvalidSearchQueryMessageList())}");
        }

        if (searchOutcome.HasUnsupportedQuery)
        {
            throw new InvalidOperationException(
                $"Query '{resourceTypeName}?{queryString}' contains unsupported parameters and would not be measured " +
                $"as written: {string.Join("; ", searchOutcome.UnsupportedQueryMessageList())}");
        }

        interceptor.Reset();

        var search = scope.ServiceProvider.GetRequiredService<IResourceStoreSearch>();
        ResourceStoreSearchOutcome result = await search.GetSearch(searchOutcome);

        return new QueryExecution(
            ResultCardinality: result.ResourceStoreList.Count,
            TotalCount: result.SearchTotal,
            Commands: interceptor.Captured);
    }
}
```

**Note:** if `ResourceStoreSearchOutcome`'s properties are named differently from `ResourceStoreList` / `SearchTotal`, read `Abm.Pyro.Domain/SearchQuery/ResourceStoreSearchOutcome.cs` and use the real names. The constructor call in `ResourceStoreSearch.cs:70-76` names both.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter QueryRunnerTest`
Expected: PASS — four tests.

- [ ] **Step 6: Commit**

```bash
git add src/Abm.Pyro.Performance/Measurement src/Abm.Pyro.Performance.Test/Integration
git commit -m "feat: capture the SQL the real search pipeline emits

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 7: The plan analyser

Logical reads are the primary metric because they are hardware-independent (spec §10). Plan operators make "seek, not scan, on index X" a mechanical assertion rather than a human reading a plan by eye.

Both parsers are pure functions over text, so they are tested without a database against committed fixtures. **Spec §13 is absolute here: a plan that fails to parse fails loudly and never records a null or defaulted metric.**

**Files:**
- Create: `src/Abm.Pyro.Performance/Measurement/StatisticsIoParser.cs`
- Create: `src/Abm.Pyro.Performance/Measurement/PlanXmlParser.cs`
- Create: `src/Abm.Pyro.Performance/Measurement/PlanAnalyser.cs`
- Create: `src/Abm.Pyro.Performance.Test/Measurement/StatisticsIoParserTest.cs`
- Create: `src/Abm.Pyro.Performance.Test/Measurement/PlanXmlParserTest.cs`
- Create: `src/Abm.Pyro.Performance.Test/Assets/sample-plan-seek.xml`
- Create: `src/Abm.Pyro.Performance.Test/Assets/sample-plan-scan.xml`

**Interfaces:**
- Consumes: `CapturedCommand` (Task 6), `CorpusHost` (Task 4).
- Produces:
  - `StatisticsIoParser.Parse(string infoMessage)` → `IReadOnlyDictionary<string, long>` (table name → logical reads).
  - `PlanXmlParser.Parse(string planXml)` → `IReadOnlyList<PlanOperator>`, `record PlanOperator(string PhysicalOp, string LogicalOp, string? IndexName, string? TableName)`.
  - `PlanAnalyser(CorpusHost host)` with `Task<PlanMeasurement> MeasureAsync(CapturedCommand command)`, `record PlanMeasurement(IReadOnlyDictionary<string, long> LogicalReadsByTable, IReadOnlyList<PlanOperator> Operators)`.

- [ ] **Step 1: Create the committed plan fixtures**

Capture two real plans from the seeded container rather than inventing XML, so the parser is tested against what SQL Server genuinely emits. In SSMS or `sqlcmd` against the `pyro-perf-corpus` container:

```sql
SET SHOWPLAN_XML ON;
GO
SELECT TOP 10 * FROM dbo.ResourceStore WHERE ResourceId = 'pat-00000001' AND ResourceType = 16;
GO
SET SHOWPLAN_XML OFF;
```

Save the returned XML to `src/Abm.Pyro.Performance.Test/Assets/sample-plan-seek.xml`. Repeat with a query that forces a scan (`WHERE Json IS NOT NULL`) and save to `sample-plan-scan.xml`. Both files go in git — they are the parser's evidence and must not be regenerated casually.

- [ ] **Step 2: Write the failing parser tests**

`src/Abm.Pyro.Performance.Test/Measurement/StatisticsIoParserTest.cs`:

```csharp
using Abm.Pyro.Performance.Measurement;

namespace Abm.Pyro.Performance.Test.Measurement;

public class StatisticsIoParserTest
{
    [Fact]
    public void Parse_ExtractsLogicalReadsPerTable()
    {
        const string message =
            "Table 'IndexString'. Scan count 1, logical reads 1234, physical reads 0, page server reads 0, read-ahead reads 0.";

        IReadOnlyDictionary<string, long> reads = StatisticsIoParser.Parse(message);

        Assert.Equal(1234, reads["IndexString"]);
    }

    [Fact]
    public void Parse_HandlesSeveralTablesInOneMessage()
    {
        string message = string.Join(Environment.NewLine,
            "Table 'ResourceStore'. Scan count 1, logical reads 500, physical reads 0.",
            "Table 'IndexString'. Scan count 3, logical reads 1234, physical reads 2.",
            "Table 'Worktable'. Scan count 0, logical reads 0, physical reads 0.");

        IReadOnlyDictionary<string, long> reads = StatisticsIoParser.Parse(message);

        Assert.Equal(500, reads["ResourceStore"]);
        Assert.Equal(1234, reads["IndexString"]);
        Assert.Equal(0, reads["Worktable"]);
    }

    [Fact]
    public void Parse_SumsRepeatedEntriesForTheSameTable()
    {
        string message = string.Join(Environment.NewLine,
            "Table 'IndexToken'. Scan count 1, logical reads 100, physical reads 0.",
            "Table 'IndexToken'. Scan count 1, logical reads 50, physical reads 0.");

        IReadOnlyDictionary<string, long> reads = StatisticsIoParser.Parse(message);

        Assert.Equal(150, reads["IndexToken"]);
    }

    [Fact]
    public void Parse_ThrowsOnAMessageThatMatchesNothing()
    {
        // Spec §13: never record a null or defaulted metric. An unrecognised STATISTICS IO
        // message means the measurement did not happen, and must be indistinguishable from
        // any other failure.
        Assert.Throws<FormatException>(() => StatisticsIoParser.Parse("Command(s) completed successfully."));
    }
}
```

`src/Abm.Pyro.Performance.Test/Measurement/PlanXmlParserTest.cs`:

```csharp
using Abm.Pyro.Performance.Measurement;

namespace Abm.Pyro.Performance.Test.Measurement;

public class PlanXmlParserTest
{
    [Fact]
    public void Parse_FindsASeekInTheSeekPlan()
    {
        string xml = File.ReadAllText(Path.Combine("Assets", "sample-plan-seek.xml"));

        IReadOnlyList<PlanOperator> operators = PlanXmlParser.Parse(xml);

        Assert.Contains(operators, op => op.PhysicalOp.Contains("Seek", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_FindsAScanInTheScanPlan()
    {
        string xml = File.ReadAllText(Path.Combine("Assets", "sample-plan-scan.xml"));

        IReadOnlyList<PlanOperator> operators = PlanXmlParser.Parse(xml);

        Assert.Contains(operators, op => op.PhysicalOp.Contains("Scan", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_NamesTheIndexEachOperatorUsed()
    {
        string xml = File.ReadAllText(Path.Combine("Assets", "sample-plan-seek.xml"));

        IReadOnlyList<PlanOperator> operators = PlanXmlParser.Parse(xml);

        Assert.Contains(operators, op => op.IndexName is not null);
    }

    [Fact]
    public void Parse_ThrowsOnMalformedXml()
    {
        // Spec §13: "Plan XML fails to parse -> fail loudly. Never record a null or defaulted metric."
        Assert.ThrowsAny<Exception>(() => PlanXmlParser.Parse("<not-a-plan>"));
    }

    [Fact]
    public void Parse_ThrowsOnWellFormedXmlThatIsNotAPlan()
    {
        Assert.Throws<FormatException>(() => PlanXmlParser.Parse("<root><child/></root>"));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "StatisticsIoParserTest|PlanXmlParserTest"`
Expected: FAIL — neither parser exists.

- [ ] **Step 4: Implement `StatisticsIoParser`**

`src/Abm.Pyro.Performance/Measurement/StatisticsIoParser.cs`:

```csharp
using System.Text.RegularExpressions;

namespace Abm.Pyro.Performance.Measurement;

/// <summary>
/// Turns SQL Server's SET STATISTICS IO output into per-table logical reads — the harness's
/// primary metric, because it is hardware-independent and reproduces exactly between runs
/// (spec §10, §16.4).
/// </summary>
public static partial class StatisticsIoParser
{
    [GeneratedRegex(@"Table '(?<table>[^']+)'\.\s*Scan count \d+,\s*logical reads (?<reads>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex StatisticsLine();

    public static IReadOnlyDictionary<string, long> Parse(string infoMessage)
    {
        var reads = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (Match match in StatisticsLine().Matches(infoMessage))
        {
            string table = match.Groups["table"].Value;
            long value = long.Parse(match.Groups["reads"].Value);
            reads[table] = reads.GetValueOrDefault(table) + value;
        }

        if (reads.Count == 0)
        {
            throw new FormatException(
                "No STATISTICS IO lines were found in the message. Recording a zero here would be a " +
                $"silently wrong measurement (spec §13). Message was: {infoMessage}");
        }

        return reads;
    }
}
```

- [ ] **Step 5: Implement `PlanXmlParser`**

`src/Abm.Pyro.Performance/Measurement/PlanXmlParser.cs`:

```csharp
using System.Xml.Linq;

namespace Abm.Pyro.Performance.Measurement;

public sealed record PlanOperator(string PhysicalOp, string LogicalOp, string? IndexName, string? TableName);

/// <summary>
/// Extracts the operator list from a SQL Server showplan, so that "seek, not scan, on index X" is
/// a mechanical assertion rather than a human reading a plan by eye (spec §10).
/// </summary>
public static class PlanXmlParser
{
    private static readonly XNamespace ShowPlan = "http://schemas.microsoft.com/sqlserver/2004/07/showplan";

    public static IReadOnlyList<PlanOperator> Parse(string planXml)
    {
        XDocument document = XDocument.Parse(planXml);

        var operators = document
            .Descendants(ShowPlan + "RelOp")
            .Select(relOp => new PlanOperator(
                PhysicalOp: relOp.Attribute("PhysicalOp")?.Value ?? string.Empty,
                LogicalOp: relOp.Attribute("LogicalOp")?.Value ?? string.Empty,
                IndexName: relOp.Descendants(ShowPlan + "Object").FirstOrDefault()?.Attribute("Index")?.Value?.Trim('[', ']'),
                TableName: relOp.Descendants(ShowPlan + "Object").FirstOrDefault()?.Attribute("Table")?.Value?.Trim('[', ']')))
            .ToList();

        if (operators.Count == 0)
        {
            throw new FormatException(
                "The document parsed as XML but contained no ShowPlan RelOp elements. Recording an empty " +
                "operator list here would let a query be reported as measured when it was not (spec §13).");
        }

        return operators;
    }
}
```

- [ ] **Step 6: Implement `PlanAnalyser`**

`src/Abm.Pyro.Performance/Measurement/PlanAnalyser.cs`:

```csharp
using System.Data;
using Abm.Pyro.Performance.Hosting;
using Microsoft.Data.SqlClient;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Measurement;

public sealed record PlanMeasurement(
    IReadOnlyDictionary<string, long> LogicalReadsByTable,
    IReadOnlyList<PlanOperator> Operators);

/// <summary>
/// Re-executes a captured command under SET STATISTICS IO, XML ON. The command text and its
/// parameters come from the production pipeline, never from hand-written SQL (spec §10).
/// </summary>
public sealed class PlanAnalyser(CorpusHost host)
{
    public async Task<PlanMeasurement> MeasureAsync(CapturedCommand command)
    {
        await using var connection = new SqlConnection(host.ConnectionString);

        var infoMessages = new List<string>();
        connection.InfoMessage += (_, args) => infoMessages.Add(args.Message);
        connection.FireInfoMessageEventOnUserErrors = false;

        await connection.OpenAsync();

        await ExecuteNonQueryAsync(connection, "SET STATISTICS IO ON; SET STATISTICS XML ON;");

        string planXml;
        await using (SqlCommand measured = BuildCommand(connection, command))
        await using (SqlDataReader reader = await measured.ExecuteReaderAsync())
        {
            planXml = await ReadPlanXmlAsync(reader);
        }

        await ExecuteNonQueryAsync(connection, "SET STATISTICS IO OFF; SET STATISTICS XML OFF;");

        if (string.IsNullOrWhiteSpace(planXml))
        {
            throw new InvalidOperationException(
                "No showplan XML was returned for the captured command. The measurement did not happen " +
                $"and must not be recorded (spec §13). Command was: {command.CommandText}");
        }

        return new PlanMeasurement(
            StatisticsIoParser.Parse(string.Join(Environment.NewLine, infoMessages)),
            PlanXmlParser.Parse(planXml));
    }

    /// <summary>
    /// Cold measurement. Destructive to the whole instance's buffer pool, which is why it is only
    /// ever run against the harness's own container (spec §10).
    /// </summary>
    public async Task DropCleanBuffersAsync()
    {
        await using var connection = new SqlConnection(host.ConnectionString);
        await connection.OpenAsync();
        await ExecuteNonQueryAsync(connection, "CHECKPOINT; DBCC DROPCLEANBUFFERS WITH NO_INFOMSGS;");
    }

    private static SqlCommand BuildCommand(SqlConnection connection, CapturedCommand captured)
    {
        SqlCommand command = connection.CreateCommand();
        command.CommandText = captured.CommandText;
        command.CommandTimeout = 300;

        foreach ((string name, object? value) in captured.Parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return command;
    }

    /// <summary>
    /// With STATISTICS XML ON, the plan arrives as an extra result set after the query's own.
    /// Walk to the last result set and take its single string column.
    /// </summary>
    private static async Task<string> ReadPlanXmlAsync(SqlDataReader reader)
    {
        string planXml = string.Empty;

        do
        {
            while (await reader.ReadAsync())
            {
                if (reader.FieldCount == 1 && reader.GetFieldType(0) == typeof(string))
                {
                    string candidate = reader.GetString(0);
                    if (candidate.Contains("ShowPlanXML", StringComparison.Ordinal))
                    {
                        planXml = candidate;
                    }
                }
            }
        }
        while (await reader.NextResultAsync());

        return planXml;
    }

    private static async Task ExecuteNonQueryAsync(SqlConnection connection, string sql)
    {
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "StatisticsIoParserTest|PlanXmlParserTest"`
Expected: PASS — nine tests, no database required.

- [ ] **Step 8: Commit**

```bash
git add src/Abm.Pyro.Performance/Measurement src/Abm.Pyro.Performance.Test/Measurement src/Abm.Pyro.Performance.Test/Assets
git commit -m "feat: parse logical reads and plan operators, failing loudly on either

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 8: The query set and the baseline store

The query set is spec §12, one entry per access pattern, each in hot and cold variants. **`_has` with a `:missing` terminal parameter is deliberately absent**: it is rejected 400 by the `_has` grammar parser before reaching the predicate layer, pinned as present behaviour in `Abm.Pyro.Api.Test/Chaining/ChainedMissingSearchTests.cs`. Chained `:missing` does work and is included.

**Files:**
- Create: `assets/perf/queries/query-set.json`
- Create: `src/Abm.Pyro.Performance/Baselines/BaselineEntry.cs`
- Create: `src/Abm.Pyro.Performance/Baselines/Baseline.cs`
- Create: `src/Abm.Pyro.Performance/Baselines/BaselineStore.cs`
- Create: `src/Abm.Pyro.Performance/Baselines/QuerySetLoader.cs`
- Create: `src/Abm.Pyro.Performance.Test/Baselines/BaselineStoreTest.cs`
- Create: `src/Abm.Pyro.Performance.Test/Baselines/QuerySetLoaderTest.cs`

**Interfaces:**
- Consumes: `CorpusManifest` (Task 1).
- Produces:
  - `record QueryDefinition(string Name, string ResourceType, string Query, string Pattern, string Temperature)` where `Temperature` is `"hot"` or `"cold"`.
  - `QuerySetLoader.Load(string path)` → `IReadOnlyList<QueryDefinition>`.
  - `record BaselineEntry(string Name, string Pattern, string Temperature, int ResultCardinality, IReadOnlyDictionary<string, long> LogicalReadsByTable, IReadOnlyList<string> PlanOperators, double ElapsedMedianMs, double ElapsedP95Ms, double CpuMedianMs, bool IsCold, bool IsSuspiciousZeroRow)`.
  - `record Baseline(string Name, CorpusManifest Corpus, string GitCommit, IReadOnlyList<BaselineEntry> Entries)`.
  - `BaselineStore.Save(Baseline, string directory)`, `BaselineStore.Load(string name, string directory)`.

- [ ] **Step 1: Write the query set**

`assets/perf/queries/query-set.json` — every pattern in spec §12, each with a hot and a cold variant so both ends of every distribution are measured rather than one accidental point (spec §8.2):

```json
{
  "queries": [
    { "name": "string-prefix-hot",        "resourceType": "Patient",     "query": "family=Smi",                                  "pattern": "string-prefix",   "temperature": "hot" },
    { "name": "string-prefix-cold",       "resourceType": "Patient",     "query": "family=Tailname0499",                         "pattern": "string-prefix",   "temperature": "cold" },
    { "name": "string-exact-hot",         "resourceType": "Patient",     "query": "family:exact=smith",                          "pattern": "string-exact",    "temperature": "hot" },
    { "name": "string-exact-cold",        "resourceType": "Patient",     "query": "family:exact=tailname0499",                   "pattern": "string-exact",    "temperature": "cold" },
    { "name": "string-contains-hot",      "resourceType": "Patient",     "query": "family:contains=mit",                         "pattern": "string-contains", "temperature": "hot" },
    { "name": "string-contains-cold",     "resourceType": "Patient",     "query": "family:contains=e0499",                       "pattern": "string-contains", "temperature": "cold" },

    { "name": "token-code-hot",           "resourceType": "Observation", "query": "code=10000-0",                                "pattern": "token-code",      "temperature": "hot" },
    { "name": "token-code-cold",          "resourceType": "Observation", "query": "code=11999-9",                               "pattern": "token-code",      "temperature": "cold" },
    { "name": "token-system-code-hot",    "resourceType": "Observation", "query": "code=http://loinc.org|10000-0",              "pattern": "token-sys-code",  "temperature": "hot" },
    { "name": "token-system-code-cold",   "resourceType": "Observation", "query": "code=http://loinc.org|11999-9",              "pattern": "token-sys-code",  "temperature": "cold" },
    { "name": "token-system-only-hot",    "resourceType": "Observation", "query": "code=http://loinc.org|",                     "pattern": "token-sys-only",  "temperature": "hot" },
    { "name": "token-not-hot",            "resourceType": "Observation", "query": "code:not=10000-0",                            "pattern": "token-not",       "temperature": "hot" },
    { "name": "token-not-cold",           "resourceType": "Observation", "query": "code:not=11999-9",                            "pattern": "token-not",       "temperature": "cold" },

    { "name": "missing-string-true",      "resourceType": "Patient",     "query": "family:missing=true",                         "pattern": "missing-string",  "temperature": "hot" },
    { "name": "missing-string-false",     "resourceType": "Patient",     "query": "family:missing=false",                        "pattern": "missing-string",  "temperature": "cold" },
    { "name": "missing-token-true",       "resourceType": "Observation", "query": "code:missing=true",                           "pattern": "missing-token",   "temperature": "hot" },
    { "name": "missing-token-false",      "resourceType": "Observation", "query": "code:missing=false",                          "pattern": "missing-token",   "temperature": "cold" },
    { "name": "missing-reference-true",   "resourceType": "Observation", "query": "subject:missing=true",                        "pattern": "missing-ref",     "temperature": "hot" },
    { "name": "missing-date-true",        "resourceType": "Patient",     "query": "birthdate:missing=true",                      "pattern": "missing-date",    "temperature": "hot" },
    { "name": "missing-quantity-true",    "resourceType": "Observation", "query": "value-quantity:missing=true",                 "pattern": "missing-qty",     "temperature": "hot" },
    { "name": "missing-uri-true",         "resourceType": "Patient",     "query": "_profile:missing=true",                       "pattern": "missing-uri",     "temperature": "hot" },
    { "name": "missing-near-true",        "resourceType": "Location",    "query": "near:missing=true",                           "pattern": "missing-near",    "temperature": "hot" },

    { "name": "reference-direct-hot",     "resourceType": "Observation", "query": "subject=Patient/pat-00000001",                "pattern": "reference",       "temperature": "hot" },
    { "name": "reference-multi-id",       "resourceType": "Observation", "query": "subject=Patient/pat-00000001,Patient/pat-00000002,Patient/pat-00000003", "pattern": "reference-in", "temperature": "hot" },

    { "name": "chained-hot",              "resourceType": "Observation", "query": "subject.family=Smith",                        "pattern": "chained",         "temperature": "hot" },
    { "name": "chained-cold",             "resourceType": "Observation", "query": "subject.family=Tailname0499",                 "pattern": "chained",         "temperature": "cold" },
    { "name": "chained-missing",          "resourceType": "Observation", "query": "subject.family:missing=true",                 "pattern": "chained-missing", "temperature": "hot" },
    { "name": "has-hot",                  "resourceType": "Patient",     "query": "_has:Observation:subject:code=10000-0",       "pattern": "has",             "temperature": "hot" },
    { "name": "has-cold",                 "resourceType": "Patient",     "query": "_has:Observation:subject:code=11999-9",      "pattern": "has",             "temperature": "cold" },

    { "name": "date-eq",                  "resourceType": "Observation", "query": "date=2025-12-31",                             "pattern": "date-eq",         "temperature": "hot" },
    { "name": "date-range-wide",          "resourceType": "Observation", "query": "date=ge2024-01-01&date=le2026-01-01",         "pattern": "date-range",      "temperature": "hot" },
    { "name": "date-range-narrow",        "resourceType": "Observation", "query": "date=ge2025-12-01&date=le2025-12-07",         "pattern": "date-range",      "temperature": "cold" },
    { "name": "date-gt",                  "resourceType": "Observation", "query": "date=gt2025-06-01",                           "pattern": "date-gt",         "temperature": "hot" },

    { "name": "quantity-with-code",       "resourceType": "Observation", "query": "value-quantity=gt100|http://unitsofmeasure.org|mg/dL", "pattern": "quantity", "temperature": "hot" },
    { "name": "quantity-narrow",          "resourceType": "Observation", "query": "value-quantity=gt199|http://unitsofmeasure.org|mg/dL", "pattern": "quantity", "temperature": "cold" },

    { "name": "uri-exact",                "resourceType": "Patient",     "query": "_profile:exact=http://hl7.org/fhir/StructureDefinition/Patient", "pattern": "uri-exact", "temperature": "cold" },
    { "name": "uri-below",                "resourceType": "Patient",     "query": "_profile:below=http://hl7.org/fhir",          "pattern": "uri-below",       "temperature": "cold" },

    { "name": "near-no-distance",         "resourceType": "Location",    "query": "near=-33.8688|151.2093",                      "pattern": "near",            "temperature": "hot" },
    { "name": "near-with-distance",       "resourceType": "Location",    "query": "near=-33.8688|151.2093|5|km",                 "pattern": "near",            "temperature": "cold" },
    { "name": "near-chained",             "resourceType": "Encounter",   "query": "location.near=-33.8688|151.2093|5|km",        "pattern": "near-chained",    "temperature": "cold" },

    { "name": "paging-page-1",            "resourceType": "Observation", "query": "code=10000-0&_count=20&_page=1",              "pattern": "paging",          "temperature": "hot" },
    { "name": "paging-deep-page",         "resourceType": "Observation", "query": "code=10000-0&_count=20&_page=200",            "pattern": "paging-deep",     "temperature": "cold" },

    { "name": "include-subject",          "resourceType": "Observation", "query": "code=10000-0&_include=Observation:subject",   "pattern": "include",         "temperature": "hot" },

    { "name": "count-versus-page",        "resourceType": "Observation", "query": "code=10000-0&_count=20",                      "pattern": "count-page-pair", "temperature": "hot" }
  ]
}
```

**Note on `count-versus-page`:** this entry is the one that exposes the double execution of spec §2.2c. It needs no special handling — `QueryRunner` already captures the `COUNT` and the paged `SELECT` as separate commands, so the baseline records both and the pair is directly readable.

- [ ] **Step 2: Write the failing baseline-store test**

`src/Abm.Pyro.Performance.Test/Baselines/BaselineStoreTest.cs`:

```csharp
using Abm.Pyro.Performance.Baselines;
using Abm.Pyro.Performance.Generation;

namespace Abm.Pyro.Performance.Test.Baselines;

public class BaselineStoreTest : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"pyro-baseline-{Guid.CreateVersion7()}");

    public BaselineStoreTest() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static Baseline SampleBaseline(string name) => new(
        Name: name,
        Corpus: new CorpusManifest(CorpusManifest.CurrentGeneratorVersion, 42, 250000, "default"),
        GitCommit: "abc1234",
        Entries:
        [
            new BaselineEntry(
                Name: "string-prefix-hot",
                Pattern: "string-prefix",
                Temperature: "hot",
                ResultCardinality: 4821,
                LogicalReadsByTable: new Dictionary<string, long> { ["IndexString"] = 1234, ["ResourceStore"] = 56 },
                PlanOperators: ["Index Seek", "Nested Loops"],
                ElapsedMedianMs: 12.4,
                ElapsedP95Ms: 18.1,
                CpuMedianMs: 9.0,
                IsCold: false,
                IsSuspiciousZeroRow: false),
        ]);

    [Fact]
    public void SaveThenLoad_RoundTripsEveryField()
    {
        BaselineStore.Save(SampleBaseline("pre-phase-b"), _directory);

        Baseline loaded = BaselineStore.Load("pre-phase-b", _directory);

        Assert.Equal("pre-phase-b", loaded.Name);
        Assert.Equal(250000, loaded.Corpus.Scale);
        Assert.Single(loaded.Entries);
        Assert.Equal(4821, loaded.Entries[0].ResultCardinality);
        Assert.Equal(1234, loaded.Entries[0].LogicalReadsByTable["IndexString"]);
        Assert.Contains("Index Seek", loaded.Entries[0].PlanOperators);
    }

    [Fact]
    public void Load_ThrowsWhenTheBaselineDoesNotExist()
    {
        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(
            () => BaselineStore.Load("never-taken", _directory));

        Assert.Contains("never-taken", exception.Message);
    }
}
```

`src/Abm.Pyro.Performance.Test/Baselines/QuerySetLoaderTest.cs`:

```csharp
using Abm.Pyro.Performance.Baselines;

namespace Abm.Pyro.Performance.Test.Baselines;

public class QuerySetLoaderTest
{
    private static string QuerySetPath =>
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "perf", "queries", "query-set.json");

    [Fact]
    public void Load_ReadsTheCommittedQuerySet()
    {
        IReadOnlyList<QueryDefinition> queries = QuerySetLoader.Load(QuerySetPath);

        Assert.NotEmpty(queries);
    }

    [Fact]
    public void Load_CoversEveryPatternSpecSectionTwelveRequires()
    {
        IReadOnlyList<QueryDefinition> queries = QuerySetLoader.Load(QuerySetPath);
        HashSet<string> patterns = queries.Select(x => x.Pattern).ToHashSet(StringComparer.Ordinal);

        string[] required =
        [
            "string-prefix", "string-exact", "string-contains",
            "token-code", "token-sys-code", "token-sys-only", "token-not",
            "missing-string", "missing-token", "missing-ref", "missing-date",
            "missing-qty", "missing-uri", "missing-near",
            "reference", "reference-in", "chained", "has",
            "date-eq", "date-range", "date-gt",
            "quantity", "uri-exact", "uri-below",
            "near", "near-chained",
            "paging", "paging-deep", "include", "count-page-pair",
        ];

        foreach (string pattern in required)
        {
            Assert.Contains(pattern, patterns);
        }
    }

    [Fact]
    public void Load_GivesEveryQueryAUniqueName()
    {
        IReadOnlyList<QueryDefinition> queries = QuerySetLoader.Load(QuerySetPath);

        Assert.Equal(queries.Count, queries.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Load_DoesNotAskForHasWithAMissingTerminalParameter()
    {
        // _has with a ':missing' terminal is rejected 400 by the _has grammar parser before it
        // reaches the predicate layer — a parser limitation, pinned in
        // Abm.Pyro.Api.Test/Chaining/ChainedMissingSearchTests.cs. Including it here would make
        // every run fail on an input the server does not accept.
        IReadOnlyList<QueryDefinition> queries = QuerySetLoader.Load(QuerySetPath);

        Assert.DoesNotContain(queries, query =>
            query.Query.Contains("_has", StringComparison.Ordinal) &&
            query.Query.Contains(":missing", StringComparison.Ordinal));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "BaselineStoreTest|QuerySetLoaderTest"`
Expected: FAIL — neither type exists.

- [ ] **Step 4: Implement the baseline records and store**

`src/Abm.Pyro.Performance/Baselines/BaselineEntry.cs`:

```csharp
namespace Abm.Pyro.Performance.Baselines;

/// <summary>
/// One measured query.
///
/// ResultCardinality is a first-class field, not a footnote (spec §10.1). Sub-project B2
/// deliberately changes result sets — the default string search narrows once the EndsWith branch
/// goes — and D may change them if a drop or collation change is ever mis-specified. Without
/// recorded cardinality a changed result set reads as a performance delta and the programme
/// draws exactly the wrong conclusion.
/// </summary>
public sealed record BaselineEntry(
    string Name,
    string Pattern,
    string Temperature,
    int ResultCardinality,
    IReadOnlyDictionary<string, long> LogicalReadsByTable,
    IReadOnlyList<string> PlanOperators,
    double ElapsedMedianMs,
    double ElapsedP95Ms,
    double CpuMedianMs,
    bool IsCold,
    bool IsSuspiciousZeroRow);
```

`src/Abm.Pyro.Performance/Baselines/Baseline.cs`:

```csharp
using Abm.Pyro.Performance.Generation;

namespace Abm.Pyro.Performance.Baselines;

/// <summary>
/// A whole measured run. The corpus manifest travels with it so a later comparison can refuse a
/// cross-corpus diff, and the git commit travels with it so a result can be traced to the code
/// that produced it. These files are the most valuable artifact in the repository — every later
/// claim in the programme rests on them (spec §5.3).
/// </summary>
public sealed record Baseline(
    string Name,
    CorpusManifest Corpus,
    string GitCommit,
    IReadOnlyList<BaselineEntry> Entries);
```

`src/Abm.Pyro.Performance/Baselines/BaselineStore.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Abm.Pyro.Performance.Baselines;

public static class BaselineStore
{
    public const string DefaultDirectory = "assets/perf/baselines";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static void Save(Baseline baseline, string directory)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(PathFor(baseline.Name, directory), JsonSerializer.Serialize(baseline, Options));
    }

    public static Baseline Load(string name, string directory)
    {
        string path = PathFor(name, directory);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No baseline named '{name}' exists at {path}.", path);
        }

        return JsonSerializer.Deserialize<Baseline>(File.ReadAllText(path), Options)
               ?? throw new InvalidDataException($"Baseline '{name}' at {path} deserialized to null.");
    }

    private static string PathFor(string name, string directory) => Path.Combine(directory, $"{name}.json");
}
```

`src/Abm.Pyro.Performance/Baselines/QuerySetLoader.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Abm.Pyro.Performance.Baselines;

public sealed record QueryDefinition(
    string Name,
    string ResourceType,
    string Query,
    string Pattern,
    string Temperature);

public static class QuerySetLoader
{
    public const string DefaultPath = "assets/perf/queries/query-set.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private sealed record QuerySetFile(IReadOnlyList<QueryDefinition> Queries);

    public static IReadOnlyList<QueryDefinition> Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No query set found at {path}.", path);
        }

        QuerySetFile file = JsonSerializer.Deserialize<QuerySetFile>(File.ReadAllText(path), Options)
                            ?? throw new InvalidDataException($"Query set at {path} deserialized to null.");

        if (file.Queries.Count == 0)
        {
            throw new InvalidDataException($"Query set at {path} contains no queries.");
        }

        return file.Queries;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter "BaselineStoreTest|QuerySetLoaderTest"`
Expected: PASS — six tests.

If `Load_CoversEveryPatternSpecSectionTwelveRequires` fails, a pattern is missing from the JSON. Add the query rather than removing the requirement — the list in the test is spec §12.

- [ ] **Step 6: Commit**

```bash
git add assets/perf/queries src/Abm.Pyro.Performance/Baselines src/Abm.Pyro.Performance.Test/Baselines
git commit -m "feat: the spec 12 query set and the baseline store

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 9: The `run` command

Ties Tasks 4–8 together: refuse a mismatched corpus, execute every query in the set N times, discard the first iteration (compilation), record median and p95, measure cold and warm, flag zero-row results, and write a baseline.

**Files:**
- Create: `src/Abm.Pyro.Performance/Measurement/QueryMeasurer.cs`
- Create: `src/Abm.Pyro.Performance/Cli/RunCommand.cs`
- Modify: `src/Abm.Pyro.Performance/Program.cs`
- Create: `src/Abm.Pyro.Performance.Test/Integration/SmokeSeedAndRunTest.cs`

**Interfaces:**
- Consumes: `QueryRunner`, `PlanAnalyser` (Tasks 6–7), `QueryDefinition`, `BaselineEntry`, `Baseline`, `BaselineStore` (Task 8), `CorpusHost` (Task 4).
- Produces: `QueryMeasurer(CorpusHost host)` with `Task<BaselineEntry> MeasureAsync(QueryDefinition query, int iterations, bool cold)`.

- [ ] **Step 1: Write the failing end-to-end smoke test**

`src/Abm.Pyro.Performance.Test/Integration/SmokeSeedAndRunTest.cs`:

```csharp
using Abm.Pyro.Performance.Baselines;
using Abm.Pyro.Performance.Generation;
using Abm.Pyro.Performance.Hosting;
using Abm.Pyro.Performance.Measurement;
using Abm.Pyro.Performance.Seeding;

namespace Abm.Pyro.Performance.Test.Integration;

[Trait("Category", "Integration")]
public class SmokeSeedAndRunTest
{
    [Fact]
    public async Task SeedThenRun_ProducesABaselineFile()
    {
        // Spec §14: "An integration test that seeds 1k resources, runs one query, and asserts a
        // baseline file is produced."
        string directory = Path.Combine(Path.GetTempPath(), $"pyro-smoke-{Guid.CreateVersion7()}");
        Directory.CreateDirectory(directory);

        try
        {
            await using CorpusHost host = await CorpusHost.StartAsync("pyro-perf-smokerun", memoryLimitMegabytes: 2048);
            await host.ApplyMigrationsAsync();
            host.StartHost();

            var manifest = new CorpusManifest(CorpusManifest.CurrentGeneratorVersion, 42, 1000, "default");
            var writer = new BulkIndexWriter(host.ConnectionString, host.Services);

            if (await host.ReadStoredManifestAsync() is null)
            {
                await writer.WriteAsync(
                    CorpusGenerator.Generate(manifest, DistributionProfile.Default()),
                    batchSize: 500,
                    CancellationToken.None);
                await host.WriteStoredManifestAsync(manifest);
            }

            var measurer = new QueryMeasurer(host);
            BaselineEntry entry = await measurer.MeasureAsync(
                new QueryDefinition("string-prefix-hot", "Patient", "family=Smi", "string-prefix", "hot"),
                iterations: 3,
                cold: false);

            BaselineStore.Save(new Baseline("smoke", manifest, "test", [entry]), directory);

            Assert.True(File.Exists(Path.Combine(directory, "smoke.json")));

            Baseline loaded = BaselineStore.Load("smoke", directory);
            Assert.Single(loaded.Entries);
            Assert.NotEmpty(loaded.Entries[0].LogicalReadsByTable);
            Assert.NotEmpty(loaded.Entries[0].PlanOperators);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task MeasureAsync_FlagsAZeroRowQueryAsSuspicious()
    {
        // Spec §13: a 0-row query's timings are meaningless, so the report must say so rather
        // than presenting them as a fast result.
        await using CorpusHost host = await CorpusHost.StartAsync("pyro-perf-smokerun", memoryLimitMegabytes: 2048);
        await host.ApplyMigrationsAsync();
        host.StartHost();

        var measurer = new QueryMeasurer(host);
        BaselineEntry entry = await measurer.MeasureAsync(
            new QueryDefinition("no-such-name", "Patient", "family:exact=zzzznonexistent", "string-exact", "cold"),
            iterations: 2,
            cold: false);

        Assert.Equal(0, entry.ResultCardinality);
        Assert.True(entry.IsSuspiciousZeroRow);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter SmokeSeedAndRunTest`
Expected: FAIL — `QueryMeasurer` does not exist.

- [ ] **Step 3: Implement `QueryMeasurer`**

`src/Abm.Pyro.Performance/Measurement/QueryMeasurer.cs`:

```csharp
using System.Diagnostics;
using Abm.Pyro.Performance.Baselines;
using Abm.Pyro.Performance.Hosting;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Measurement;

/// <summary>
/// Runs one query definition N times and folds the results into a single baseline entry.
/// Logical reads and plan operators come from the last iteration, because they are deterministic;
/// elapsed and CPU are summarised across iterations, because they are not (spec §10, §16.4).
/// </summary>
public sealed class QueryMeasurer(CorpusHost host)
{
    public async Task<BaselineEntry> MeasureAsync(QueryDefinition query, int iterations, bool cold)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 2);

        var runner = new QueryRunner(host);
        var analyser = new PlanAnalyser(host);

        var elapsedSamples = new List<double>(iterations);
        QueryExecution? lastExecution = null;

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            if (cold)
            {
                await analyser.DropCleanBuffersAsync();
            }

            var stopwatch = Stopwatch.StartNew();
            QueryExecution execution = await runner.ExecuteAsync(query.ResourceType, query.Query);
            stopwatch.Stop();

            // The first iteration pays for plan compilation and is discarded (spec §10).
            if (iteration > 0)
            {
                elapsedSamples.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            lastExecution = execution;
        }

        QueryExecution final = lastExecution
            ?? throw new InvalidOperationException($"Query '{query.Name}' produced no execution.");

        // The paged SELECT is the command worth analysing; the COUNT is recorded too, but the
        // plan operators reported are the ones for the query that returns the page.
        CapturedCommand principal = final.Commands
            .OrderByDescending(command => command.CommandText.Length)
            .First();

        PlanMeasurement plan = await analyser.MeasureAsync(principal);

        var logicalReads = new Dictionary<string, long>(plan.LogicalReadsByTable, StringComparer.Ordinal);
        foreach (CapturedCommand command in final.Commands.Where(c => !ReferenceEquals(c, principal)))
        {
            PlanMeasurement extra = await analyser.MeasureAsync(command);
            foreach ((string table, long reads) in extra.LogicalReadsByTable)
            {
                logicalReads[table] = logicalReads.GetValueOrDefault(table) + reads;
            }
        }

        return new BaselineEntry(
            Name: query.Name,
            Pattern: query.Pattern,
            Temperature: query.Temperature,
            ResultCardinality: final.TotalCount,
            LogicalReadsByTable: logicalReads,
            PlanOperators: plan.Operators
                .Select(op => op.IndexName is null ? op.PhysicalOp : $"{op.PhysicalOp} [{op.IndexName}]")
                .ToList(),
            ElapsedMedianMs: Median(elapsedSamples),
            ElapsedP95Ms: Percentile(elapsedSamples, 0.95),
            CpuMedianMs: Median(elapsedSamples),
            IsCold: cold,
            IsSuspiciousZeroRow: final.TotalCount == 0);
    }

    private static double Median(List<double> samples)
    {
        if (samples.Count == 0)
        {
            return 0;
        }

        double[] sorted = [.. samples.Order()];
        int middle = sorted.Length / 2;

        return sorted.Length % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    private static double Percentile(List<double> samples, double percentile)
    {
        if (samples.Count == 0)
        {
            return 0;
        }

        double[] sorted = [.. samples.Order()];
        int index = (int)Math.Ceiling(percentile * sorted.Length) - 1;

        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }
}
```

- [ ] **Step 4: Implement the `run` command**

`src/Abm.Pyro.Performance/Cli/RunCommand.cs`:

```csharp
using System.CommandLine;
using System.Diagnostics;
using Abm.Pyro.Performance.Baselines;
using Abm.Pyro.Performance.Generation;
using Abm.Pyro.Performance.Hosting;
using Abm.Pyro.Performance.Measurement;

namespace Abm.Pyro.Performance.Cli;

public static class RunCommand
{
    public static Command Create()
    {
        var queriesOption = new Option<string>("--queries", () => "all", "Query name, pattern, or 'all'");
        var baselineOption = new Option<string>("--baseline", "Name to save this run under") { IsRequired = true };
        var scaleOption = new Option<int>("--scale", () => 250_000, "Corpus scale, used to verify the seeded corpus");
        var seedOption = new Option<int>("--seed", () => 42, "Corpus RNG seed, used to verify the seeded corpus");
        var profileOption = new Option<string>("--profile", () => "default", "Corpus profile");
        var iterationsOption = new Option<int>("--iterations", () => 6, "Iterations per query; the first is discarded");
        var coldOption = new Option<bool>("--cold", () => false, "Drop clean buffers before each iteration");
        var memoryOption = new Option<int>("--memory-mb", () => 4096, "SQL container memory cap");
        var containerOption = new Option<string>("--container", () => SeedCommand.DefaultContainerName, "Docker container name");

        var command = new Command("run", "Measure the query set and write a baseline")
        {
            queriesOption, baselineOption, scaleOption, seedOption, profileOption,
            iterationsOption, coldOption, memoryOption, containerOption,
        };

        command.SetHandler(async (context) =>
        {
            string queries = context.ParseResult.GetValueForOption(queriesOption)!;
            string baselineName = context.ParseResult.GetValueForOption(baselineOption)!;
            int scale = context.ParseResult.GetValueForOption(scaleOption);
            int seed = context.ParseResult.GetValueForOption(seedOption);
            string profile = context.ParseResult.GetValueForOption(profileOption)!;
            int iterations = context.ParseResult.GetValueForOption(iterationsOption);
            bool cold = context.ParseResult.GetValueForOption(coldOption);
            int memoryMb = context.ParseResult.GetValueForOption(memoryOption);
            string container = context.ParseResult.GetValueForOption(containerOption)!;

            var manifest = new CorpusManifest(CorpusManifest.CurrentGeneratorVersion, seed, scale, profile);

            await using CorpusHost host = await CorpusHost.StartAsync(container, memoryMb);

            // Never measure a corpus that is not the one requested (spec §13).
            await host.RequireManifestAsync(manifest);
            host.StartHost();

            IReadOnlyList<QueryDefinition> allQueries = QuerySetLoader.Load(QuerySetLoader.DefaultPath);
            List<QueryDefinition> selected = string.Equals(queries, "all", StringComparison.Ordinal)
                ? [.. allQueries]
                : [.. allQueries.Where(q =>
                    string.Equals(q.Name, queries, StringComparison.Ordinal) ||
                    string.Equals(q.Pattern, queries, StringComparison.Ordinal))];

            if (selected.Count == 0)
            {
                Console.Error.WriteLine($"No query in the set matches '{queries}'.");
                context.ExitCode = 1;
                return;
            }

            var measurer = new QueryMeasurer(host);
            var entries = new List<BaselineEntry>(selected.Count);

            foreach (QueryDefinition query in selected)
            {
                Console.Write($"  {query.Name,-28} ");
                BaselineEntry entry = await measurer.MeasureAsync(query, iterations, cold);
                entries.Add(entry);

                string flag = entry.IsSuspiciousZeroRow ? "  [ZERO ROWS — timings meaningless]" : string.Empty;
                Console.WriteLine(
                    $"{entry.ResultCardinality,8:N0} rows  " +
                    $"{entry.LogicalReadsByTable.Values.Sum(),10:N0} reads  " +
                    $"{entry.ElapsedMedianMs,8:N1} ms{flag}");
            }

            var baseline = new Baseline(baselineName, manifest, ReadGitCommit(), entries);
            BaselineStore.Save(baseline, BaselineStore.DefaultDirectory);

            Console.WriteLine();
            Console.WriteLine($"Baseline '{baselineName}' written to {BaselineStore.DefaultDirectory}/{baselineName}.json");

            int zeroRowCount = entries.Count(entry => entry.IsSuspiciousZeroRow);
            if (zeroRowCount > 0)
            {
                Console.WriteLine($"WARNING: {zeroRowCount} queries returned zero rows. Their timings mean nothing (spec §13).");
            }
        });

        return command;
    }

    private static string ReadGitCommit()
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo("git", "rev-parse --short HEAD")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });

            return process?.StandardOutput.ReadToEnd().Trim() ?? "unknown";
        }
        catch (Exception)
        {
            return "unknown";
        }
    }
}
```

Add to `Program.cs`:

```csharp
        root.AddCommand(Cli.RunCommand.Create());
```

- [ ] **Step 5: Run the smoke tests to verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter SmokeSeedAndRunTest`
Expected: PASS — two tests.

- [ ] **Step 6: Take the first real baseline**

Run: `dotnet run --project src/Abm.Pyro.Performance -- run --queries all --baseline pre-phase-b`
Expected: every entry in the query set is measured and `assets/perf/baselines/pre-phase-b.json` is written.

- [ ] **Step 7: Verify reproducibility — spec §16.4**

Run: `dotnet run --project src/Abm.Pyro.Performance -- run --queries all --baseline pre-phase-b-repeat`

Then compare the two files:

```bash
python -c "
import json
a = json.load(open('assets/perf/baselines/pre-phase-b.json'))
b = json.load(open('assets/perf/baselines/pre-phase-b-repeat.json'))
for x, y in zip(a['Entries'], b['Entries']):
    assert x['LogicalReadsByTable'] == y['LogicalReadsByTable'], x['Name']
    assert x['ResultCardinality'] == y['ResultCardinality'], x['Name']
print('logical reads and cardinality reproduce exactly')
"
```

Expected: logical reads reproduce **exactly** and elapsed medians land within ±15% (spec §16.4). If logical reads differ between two runs against an unchanged corpus, stop — something is non-deterministic and every later measurement is untrustworthy.

Delete `pre-phase-b-repeat.json` once verified; it is a check, not an artifact.

- [ ] **Step 8: Commit**

```bash
rm assets/perf/baselines/pre-phase-b-repeat.json
git add src/Abm.Pyro.Performance assets/perf/baselines/pre-phase-b.json
git commit -m "feat: the run command and the first committed baseline

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 10: `compare` and the report writer

**Spec §17.2, settled:** report-only for the 250k corpus, fail-on-regression for the 10k CI gate. `compare` therefore takes `--fail-on-regression`, defaulting to false.

**Files:**
- Create: `src/Abm.Pyro.Performance/Baselines/ReportWriter.cs`
- Create: `src/Abm.Pyro.Performance/Cli/CompareCommand.cs`
- Modify: `src/Abm.Pyro.Performance/Program.cs`
- Create: `src/Abm.Pyro.Performance.Test/Baselines/ReportWriterTest.cs`

**Interfaces:**
- Consumes: `Baseline`, `BaselineEntry`, `BaselineStore` (Task 8).
- Produces: `ReportWriter.Write(Baseline from, Baseline to)` → `string` (markdown); `ReportWriter.HasRegression(Baseline from, Baseline to, double tolerance)` → `bool`.

- [ ] **Step 1: Write the failing report test**

`src/Abm.Pyro.Performance.Test/Baselines/ReportWriterTest.cs`:

```csharp
using Abm.Pyro.Performance.Baselines;
using Abm.Pyro.Performance.Generation;

namespace Abm.Pyro.Performance.Test.Baselines;

public class ReportWriterTest
{
    private static readonly CorpusManifest Corpus =
        new(CorpusManifest.CurrentGeneratorVersion, 42, 250000, "default");

    private static Baseline Make(string name, CorpusManifest corpus, long reads, int cardinality) => new(
        name, corpus, "abc1234",
        [
            new BaselineEntry("string-prefix-hot", "string-prefix", "hot", cardinality,
                new Dictionary<string, long> { ["IndexString"] = reads },
                ["Index Scan [IX_IndexString_Value]"], 12.0, 18.0, 9.0, false, false),
        ]);

    [Fact]
    public void Write_ReportsAnImprovementAsANegativeDelta()
    {
        string report = ReportWriter.Write(
            Make("before", Corpus, reads: 10_000, cardinality: 500),
            Make("after", Corpus, reads: 1_000, cardinality: 500));

        Assert.Contains("string-prefix-hot", report);
        Assert.Contains("-90", report);
    }

    [Fact]
    public void Write_CallsOutAChangedResultSetSoItIsNotReadAsAPerformanceDelta()
    {
        // Spec §10.1: without this, B2's deliberate narrowing of the string search would read
        // as a performance win rather than as the result-set change it is.
        string report = ReportWriter.Write(
            Make("before", Corpus, reads: 10_000, cardinality: 500),
            Make("after", Corpus, reads: 1_000, cardinality: 120));

        Assert.Contains("CARDINALITY CHANGED", report);
    }

    [Fact]
    public void Write_RefusesToCompareBaselinesFromDifferentCorpora()
    {
        // Review Focus #4: a cross-corpus diff is meaningless and must be refused, not rendered.
        var otherCorpus = new CorpusManifest(CorpusManifest.CurrentGeneratorVersion, 42, 10000, "default");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ReportWriter.Write(
                Make("before", Corpus, reads: 10_000, cardinality: 500),
                Make("after", otherCorpus, reads: 1_000, cardinality: 500)));

        Assert.Contains("different corpora", exception.Message);
    }

    [Fact]
    public void HasRegression_IsTrueWhenLogicalReadsRiseBeyondTolerance()
    {
        bool regressed = ReportWriter.HasRegression(
            Make("before", Corpus, reads: 1_000, cardinality: 500),
            Make("after", Corpus, reads: 2_000, cardinality: 500),
            tolerance: 0.10);

        Assert.True(regressed);
    }

    [Fact]
    public void HasRegression_IsFalseForAnImprovement()
    {
        bool regressed = ReportWriter.HasRegression(
            Make("before", Corpus, reads: 2_000, cardinality: 500),
            Make("after", Corpus, reads: 1_000, cardinality: 500),
            tolerance: 0.10);

        Assert.False(regressed);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter ReportWriterTest`
Expected: FAIL — `ReportWriter` does not exist.

- [ ] **Step 3: Implement `ReportWriter`**

`src/Abm.Pyro.Performance/Baselines/ReportWriter.cs`:

```csharp
using System.Text;

namespace Abm.Pyro.Performance.Baselines;

public static class ReportWriter
{
    public static string Write(Baseline from, Baseline to)
    {
        RequireSameCorpus(from, to);

        var report = new StringBuilder();
        report.AppendLine($"# Baseline comparison: `{from.Name}` -> `{to.Name}`");
        report.AppendLine();
        report.AppendLine($"Corpus: seed {from.Corpus.Seed}, scale {from.Corpus.Scale:N0}, profile `{from.Corpus.Profile}`, hash `{from.Corpus.ContentHash[..12]}`");
        report.AppendLine($"Commits: `{from.GitCommit}` -> `{to.GitCommit}`");
        report.AppendLine();
        report.AppendLine("| Query | Reads before | Reads after | Δ reads | Median before | Median after | Δ median | Rows | Note |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---|");

        Dictionary<string, BaselineEntry> toByName = to.Entries.ToDictionary(x => x.Name, StringComparer.Ordinal);

        foreach (BaselineEntry before in from.Entries)
        {
            if (!toByName.TryGetValue(before.Name, out BaselineEntry? after))
            {
                report.AppendLine($"| {before.Name} | {Sum(before)} | — | — | {before.ElapsedMedianMs:N1} | — | — | {before.ResultCardinality:N0} | MISSING FROM `{to.Name}` |");
                continue;
            }

            long beforeReads = Sum(before);
            long afterReads = Sum(after);

            var notes = new List<string>();
            if (before.ResultCardinality != after.ResultCardinality)
            {
                // Spec §10.1: a changed result set must never be misread as a performance delta.
                notes.Add($"**CARDINALITY CHANGED** {before.ResultCardinality:N0} -> {after.ResultCardinality:N0}");
            }

            if (before.IsSuspiciousZeroRow || after.IsSuspiciousZeroRow)
            {
                notes.Add("zero rows — timings meaningless");
            }

            report.AppendLine(
                $"| {before.Name} | {beforeReads:N0} | {afterReads:N0} | {Percent(beforeReads, afterReads)} | " +
                $"{before.ElapsedMedianMs:N1} | {after.ElapsedMedianMs:N1} | {Percent(before.ElapsedMedianMs, after.ElapsedMedianMs)} | " +
                $"{after.ResultCardinality:N0} | {string.Join("; ", notes)} |");
        }

        return report.ToString();
    }

    public static bool HasRegression(Baseline from, Baseline to, double tolerance)
    {
        RequireSameCorpus(from, to);

        Dictionary<string, BaselineEntry> fromByName = from.Entries.ToDictionary(x => x.Name, StringComparer.Ordinal);

        foreach (BaselineEntry after in to.Entries)
        {
            if (!fromByName.TryGetValue(after.Name, out BaselineEntry? before))
            {
                continue;
            }

            long beforeReads = Sum(before);
            if (beforeReads == 0)
            {
                continue;
            }

            if ((Sum(after) - beforeReads) / (double)beforeReads > tolerance)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Review Focus #4. Two baselines taken against different corpora cannot be diffed: the
    /// numbers describe different data. Refuse rather than render something misleading.
    /// </summary>
    private static void RequireSameCorpus(Baseline from, Baseline to)
    {
        if (!string.Equals(from.Corpus.ContentHash, to.Corpus.ContentHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Baselines '{from.Name}' and '{to.Name}' were taken against different corpora " +
                $"({from.Corpus.Scale:N0}/seed {from.Corpus.Seed} versus {to.Corpus.Scale:N0}/seed {to.Corpus.Seed}). " +
                "A cross-corpus comparison describes different data and cannot be interpreted.");
        }
    }

    private static long Sum(BaselineEntry entry) => entry.LogicalReadsByTable.Values.Sum();

    private static string Percent(double before, double after)
    {
        if (before == 0)
        {
            return "—";
        }

        double delta = (after - before) / before * 100;
        return $"{delta:+0;-0;0}%";
    }
}
```

- [ ] **Step 4: Implement the `compare` command**

`src/Abm.Pyro.Performance/Cli/CompareCommand.cs`:

```csharp
using System.CommandLine;
using Abm.Pyro.Performance.Baselines;

namespace Abm.Pyro.Performance.Cli;

public static class CompareCommand
{
    public static Command Create()
    {
        var fromOption = new Option<string>("--from", "Baseline name to compare from") { IsRequired = true };
        var toOption = new Option<string>("--to", "Baseline name to compare to") { IsRequired = true };
        var outputOption = new Option<string?>("--output", () => null, "Write the markdown report to this file");

        // Spec §17.2, settled: report-only for the 250k corpus, fail-on-regression for the 10k
        // CI gate. The flag exists so the gate can opt in; the default keeps iteration friction low.
        var failOption = new Option<bool>("--fail-on-regression", () => false, "Exit non-zero if logical reads regress");
        var toleranceOption = new Option<double>("--tolerance", () => 0.10, "Fractional increase in logical reads tolerated");

        var command = new Command("compare", "Diff two baselines")
        {
            fromOption, toOption, outputOption, failOption, toleranceOption,
        };

        command.SetHandler((from, to, output, fail, tolerance) =>
        {
            Baseline before = BaselineStore.Load(from, BaselineStore.DefaultDirectory);
            Baseline after = BaselineStore.Load(to, BaselineStore.DefaultDirectory);

            string report = ReportWriter.Write(before, after);

            if (output is not null)
            {
                File.WriteAllText(output, report);
                Console.WriteLine($"Report written to {output}");
            }
            else
            {
                Console.WriteLine(report);
            }

            if (fail && ReportWriter.HasRegression(before, after, tolerance))
            {
                Console.Error.WriteLine($"Regression: logical reads rose by more than {tolerance:P0} on at least one query.");
                Environment.ExitCode = 1;
            }
        }, fromOption, toOption, outputOption, failOption, toleranceOption);

        return command;
    }
}
```

Add to `Program.cs`:

```csharp
        root.AddCommand(Cli.CompareCommand.Create());
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter ReportWriterTest`
Expected: PASS — five tests.

- [ ] **Step 6: Commit**

```bash
git add src/Abm.Pyro.Performance src/Abm.Pyro.Performance.Test/Baselines
git commit -m "feat: baseline comparison, refusing cross-corpus diffs

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 11: The `ingest` command

Write throughput is measured through the **real `FhirCreateHandler`**, never the bulk path (spec §9). Conflating them would let the programme claim a write-cost number the bulk path never exercised — which matters, because the aggressive index-drop posture of sub-project D must be accountable to a real ingest measurement.

**Files:**
- Create: `src/Abm.Pyro.Performance/Cli/IngestCommand.cs`
- Modify: `src/Abm.Pyro.Performance/Program.cs`

**Interfaces:**
- Consumes: `CorpusHost`, `TenantScope` (Task 4); `CorpusGenerator`, `DistributionProfile` (Tasks 2–3); production `IFhirCreateHandler` (or whatever `Abm.Pyro.Application/FhirHandler/FhirCreateHandler.cs` implements) and `FhirCreateRequest`.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Read the real create path**

Before writing anything, read `src/Abm.Pyro.Application/FhirHandler/FhirCreateHandler.cs` and `src/Abm.Pyro.Domain/FhirRequest/FhirCreateRequest.cs` to get the exact handler interface name and the exact `FhirCreateRequest` constructor parameters. The command below calls them; the names must match what is actually there.

- [ ] **Step 2: Implement the `ingest` command**

`src/Abm.Pyro.Performance/Cli/IngestCommand.cs`:

```csharp
using System.CommandLine;
using System.Diagnostics;
using Abm.Pyro.Performance.Generation;
using Abm.Pyro.Performance.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Abm.Pyro.Performance.Cli;

/// <summary>
/// Ingest throughput, measured through the real create handler.
///
/// The bulk path builds corpora; the handler path measures writes (spec §9). They are separate
/// commands precisely so a write-cost claim can never be made on the strength of a path that
/// skips the handler, the transaction behaviour, and the per-row EF insert.
/// </summary>
public static class IngestCommand
{
    public static Command Create()
    {
        var countOption = new Option<int>("--count", () => 5000, "Resources to push through the real create handler");
        var seedOption = new Option<int>("--seed", () => 9001, "RNG seed; use one that differs from the corpus seed");
        var memoryOption = new Option<int>("--memory-mb", () => 4096, "SQL container memory cap");
        var containerOption = new Option<string>("--container", () => SeedCommand.DefaultContainerName, "Docker container name");

        var command = new Command("ingest", "Measure write throughput through the real create handler")
        {
            countOption, seedOption, memoryOption, containerOption,
        };

        command.SetHandler(async (count, seed, memoryMb, container) =>
        {
            if (count < 1)
            {
                Console.Error.WriteLine($"--count must be at least 1; got {count}.");
                Environment.ExitCode = 1;
                return;
            }

            await using CorpusHost host = await CorpusHost.StartAsync(container, memoryMb);
            host.StartHost();

            var manifest = new CorpusManifest(CorpusManifest.CurrentGeneratorVersion, seed, count, "default");

            var stopwatch = Stopwatch.StartNew();
            int written = 0;

            foreach (GeneratedResource generated in CorpusGenerator.Generate(manifest, DistributionProfile.Default()))
            {
                // A fresh scope per resource, because that is what an HTTP request gets: the
                // measurement must include per-request scope construction and DbContext setup.
                using IServiceScope scope = TenantScope.Create(host.Services);

                // Resolve the create handler and dispatch the request, using the exact interface
                // and request-record shapes read in Step 1.
                await SendCreateAsync(scope.ServiceProvider, generated);
                written++;

                if (written % 500 == 0)
                {
                    Console.WriteLine($"  {written:N0} / {count:N0}  ({written / stopwatch.Elapsed.TotalSeconds:N0}/s)");
                }
            }

            stopwatch.Stop();

            Console.WriteLine();
            Console.WriteLine($"Ingested {written:N0} resources in {stopwatch.Elapsed.TotalSeconds:N1}s " +
                              $"({written / stopwatch.Elapsed.TotalSeconds:N0} resources/second).");
            Console.WriteLine("This is the figure sub-project D's index drops must be accountable to.");
        }, countOption, seedOption, memoryOption, containerOption);

        return command;
    }

    private static async Task SendCreateAsync(IServiceProvider services, GeneratedResource generated)
    {
        // Implemented in Step 3 against the real handler interface.
        throw new NotImplementedException();
    }
}
```

- [ ] **Step 3: Fill in `SendCreateAsync` against the real handler**

Replace the `NotImplementedException` body with a resolve-and-dispatch of the create handler read in Step 1. The shape will be close to:

```csharp
    private static async Task SendCreateAsync(IServiceProvider services, GeneratedResource generated)
    {
        var handler = services.GetRequiredService<IFhirCreateHandler>();

        var request = new FhirCreateRequest(
            RequestSchema: "https",
            Tenant: services.GetRequiredService<ITenantService>().GetScopedTenantCode(),
            RequestId: generated.Resource.Id!,
            ResourceName: generated.ResourceType.GetCode(),
            Resource: generated.Resource,
            Headers: new Dictionary<string, StringValues>(),
            TimeStamp: generated.LastUpdatedUtc);

        await handler.Handle(request, CancellationToken.None);
    }
```

The parameter names and order **must** be taken from the real `FhirCreateRequest` record; do not guess. If the handler is dispatched through the mediator rather than resolved directly, resolve the mediator and send the request through it instead — whichever `FhirController`'s create action does is the right one to mirror, because the point of this command is to measure the production path.

- [ ] **Step 4: Verify the command runs**

Run against a container with no corpus seeded, so the ingest measurement is not distorted by an existing 250k of data:

```bash
docker rm -f pyro-perf-ingest
dotnet run --project src/Abm.Pyro.Performance -- ingest --count 2000 --container pyro-perf-ingest
```

Expected: prints a resources/second figure. Record it in `assets/perf/measured-sizes.md` under a new "Ingest throughput" heading, with the date, the count, and a note that it is the pre-D baseline write cost.

- [ ] **Step 5: Commit**

```bash
git add src/Abm.Pyro.Performance assets/perf/measured-sizes.md
git commit -m "feat: ingest throughput through the real create handler

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 12: The index shape probe

A microbenchmark layer so six column orders can be compared in minutes without touching C#. It is explicitly a **hypothesis generator, never the evidence of record** (spec §10) — only the pipeline-driven measurement reflects the SQL EF actually produces.

**Files:**
- Create: `src/Abm.Pyro.Performance/Measurement/IndexShapeProbe.cs`
- Create: `assets/perf/queries/index-shape-probes.json`

**Interfaces:**
- Consumes: `CorpusHost` (Task 4), `StatisticsIoParser`, `PlanXmlParser` (Task 7).
- Produces: `IndexShapeProbe(CorpusHost host)` with `Task<ProbeResult> ProbeAsync(string indexName, string indexDdl, string probeSql)`, and `record ProbeResult(string IndexDdl, IReadOnlyDictionary<string, long> LogicalReadsByTable, IReadOnlyList<PlanOperator> Operators)`.

- [ ] **Step 1: Implement `IndexShapeProbe`**

`src/Abm.Pyro.Performance/Measurement/IndexShapeProbe.cs`:

```csharp
using Abm.Pyro.Performance.Hosting;
using Microsoft.Data.SqlClient;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Performance.Measurement;

public sealed record ProbeResult(
    string IndexDdl,
    IReadOnlyDictionary<string, long> LogicalReadsByTable,
    IReadOnlyList<PlanOperator> Operators);

/// <summary>
/// Creates a candidate index, measures one hand-written query against it, then drops it again.
///
/// This exists so that six column orders can be compared in minutes rather than by rebuilding and
/// re-measuring the whole pipeline. It is a HYPOTHESIS GENERATOR, never the evidence of record
/// (spec §10): a shape that wins here still has to win under QueryRunner before sub-project D may
/// cite it, because only the pipeline-driven measurement reflects the SQL EF actually produces.
///
/// It creates and drops indexes, which the production application can never do — the app holds
/// only db_datareader/db_datawriter. That is exactly why this is confined to the harness's own
/// container and why every real index change still ships as an EF migration.
/// </summary>
public sealed class IndexShapeProbe(CorpusHost host)
{
    public async Task<ProbeResult> ProbeAsync(string indexName, string indexDdl, string probeSql)
    {
        await ExecuteAsync($"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{indexName}') DROP INDEX {indexName} ON dbo.{TableFromDdl(indexDdl)};");
        await ExecuteAsync(indexDdl);

        try
        {
            var analyser = new PlanAnalyser(host);
            await analyser.DropCleanBuffersAsync();

            PlanMeasurement measurement = await analyser.MeasureAsync(
                new CapturedCommand(probeSql, new Dictionary<string, object?>(StringComparer.Ordinal), TimeSpan.Zero));

            return new ProbeResult(indexDdl, measurement.LogicalReadsByTable, measurement.Operators);
        }
        finally
        {
            await ExecuteAsync($"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{indexName}') DROP INDEX {indexName} ON dbo.{TableFromDdl(indexDdl)};");
        }
    }

    private static string TableFromDdl(string indexDdl)
    {
        // "CREATE INDEX x ON dbo.IndexString (...)" -> "IndexString"
        int onIndex = indexDdl.IndexOf(" ON ", StringComparison.OrdinalIgnoreCase);
        string afterOn = indexDdl[(onIndex + 4)..].TrimStart();
        string qualified = afterOn.Split([' ', '('], StringSplitOptions.RemoveEmptyEntries)[0];

        return qualified.Split('.').Last().Trim('[', ']');
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(host.ConnectionString);
        await connection.OpenAsync();

        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 600;
        await command.ExecuteNonQueryAsync();
    }
}
```

- [ ] **Step 2: Write the candidate shapes that answer spec §2.2d**

`assets/perf/queries/index-shape-probes.json`. The two competing hypotheses of spec §2.2d get their own entries, and each is probed against a hot value and a cold value, because the whole point is that they behave oppositely:

```json
{
  "probes": [
    {
      "name": "indexstring-value-leading",
      "indexName": "PROBE_IndexString_A",
      "indexDdl": "CREATE NONCLUSTERED INDEX PROBE_IndexString_A ON dbo.IndexString (SearchParameterStoreId, Value) INCLUDE (ResourceStoreId);",
      "probes": [
        { "temperature": "hot",  "sql": "SELECT COUNT(*) FROM dbo.ResourceStore r WHERE r.ResourceType = 16 AND r.IsCurrent = 1 AND r.IsDeleted = 0 AND EXISTS (SELECT 1 FROM dbo.IndexString i WHERE i.ResourceStoreId = r.ResourceStoreId AND i.SearchParameterStoreId = 1 AND i.Value LIKE 'smi%');" },
        { "temperature": "cold", "sql": "SELECT COUNT(*) FROM dbo.ResourceStore r WHERE r.ResourceType = 16 AND r.IsCurrent = 1 AND r.IsDeleted = 0 AND EXISTS (SELECT 1 FROM dbo.IndexString i WHERE i.ResourceStoreId = r.ResourceStoreId AND i.SearchParameterStoreId = 1 AND i.Value LIKE 'tailname0499%');" }
      ]
    },
    {
      "name": "indexstring-correlation-leading",
      "indexName": "PROBE_IndexString_B",
      "indexDdl": "CREATE NONCLUSTERED INDEX PROBE_IndexString_B ON dbo.IndexString (SearchParameterStoreId, ResourceStoreId, Value);",
      "probes": [
        { "temperature": "hot",  "sql": "SELECT COUNT(*) FROM dbo.ResourceStore r WHERE r.ResourceType = 16 AND r.IsCurrent = 1 AND r.IsDeleted = 0 AND EXISTS (SELECT 1 FROM dbo.IndexString i WHERE i.ResourceStoreId = r.ResourceStoreId AND i.SearchParameterStoreId = 1 AND i.Value LIKE 'smi%');" },
        { "temperature": "cold", "sql": "SELECT COUNT(*) FROM dbo.ResourceStore r WHERE r.ResourceType = 16 AND r.IsCurrent = 1 AND r.IsDeleted = 0 AND EXISTS (SELECT 1 FROM dbo.IndexString i WHERE i.ResourceStoreId = r.ResourceStoreId AND i.SearchParameterStoreId = 1 AND i.Value LIKE 'tailname0499%');" }
      ]
    },
    {
      "name": "resourcestore-current-by-type",
      "indexName": "PROBE_ResourceStore_A",
      "indexDdl": "CREATE NONCLUSTERED INDEX PROBE_ResourceStore_A ON dbo.ResourceStore (ResourceType, IsCurrent, IsDeleted) INCLUDE (LastUpdatedUtc);",
      "probes": [
        { "temperature": "hot", "sql": "SELECT TOP 20 ResourceStoreId FROM dbo.ResourceStore WHERE ResourceType = 33 AND IsCurrent = 1 AND IsDeleted = 0 ORDER BY LastUpdatedUtc DESC;" }
      ]
    }
  ]
}
```

**Note:** the literal `SearchParameterStoreId` and `ResourceType` values above are placeholders that must be replaced with the real ones before the probes are run. Find them by querying the seeded container: `SELECT SearchParameterStoreId, Code FROM dbo.SearchParameterStore WHERE Code = 'family';` and reading `FhirResourceTypeId` in `Abm.Pyro.Domain/Enums/`. Running a probe against the wrong parameter id measures nothing and looks like a win.

- [ ] **Step 3: Verify one probe runs end to end**

Write a throwaway `dotnet run` invocation, or a temporary xUnit fact, that constructs `IndexShapeProbe` against the seeded container and runs `indexstring-value-leading`'s hot probe. Confirm the returned `Operators` list names `PROBE_IndexString_A`, which proves the candidate index was actually used rather than ignored.

Delete the throwaway afterwards; the probe layer is driven by hand during sub-project D, not by a committed test.

- [ ] **Step 4: Commit**

```bash
git add src/Abm.Pyro.Performance/Measurement/IndexShapeProbe.cs assets/perf/queries/index-shape-probes.json
git commit -m "feat: index shape probe for candidate column orders

A hypothesis generator for sub-project D, never the evidence of record.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 13: The 10k CI gate

Spec §16.6 and §14: a gate fast enough to live inside `dotnet test`, asserting **seek-not-scan only** — never a wall-clock threshold, which would be flaky on a shared runner.

**Spec §17.1, settled:** the budget is measured, not guessed. Step 4 runs the gate, reads the real elapsed time, and writes that number into the test as the ceiling with headroom.

**Files:**
- Create: `src/Abm.Pyro.Performance.Test/Integration/CiGateTest.cs`
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: everything from Tasks 1–9.
- Produces: nothing.

- [ ] **Step 1: Write the gate test**

`src/Abm.Pyro.Performance.Test/Integration/CiGateTest.cs`:

```csharp
using System.Diagnostics;
using Abm.Pyro.Performance.Baselines;
using Abm.Pyro.Performance.Generation;
using Abm.Pyro.Performance.Hosting;
using Abm.Pyro.Performance.Measurement;
using Abm.Pyro.Performance.Seeding;

namespace Abm.Pyro.Performance.Test.Integration;

/// <summary>
/// The CI gate. It asserts plan SHAPE, never wall-clock time: a shared GitHub runner's timings
/// vary far too much to gate on, but "this query seeks" is a fact about the plan and is stable.
/// </summary>
[Trait("Category", "Integration")]
public class CiGateTest : IAsyncLifetime
{
    /// <summary>
    /// Filled in from the first measured run (spec §17.1), not guessed. See Step 4.
    /// </summary>
    private const int WallClockBudgetSeconds = 0; // REPLACE in Step 4

    private CorpusHost _host = default!;
    private CorpusManifest _manifest = default!;

    public async Task InitializeAsync()
    {
        _manifest = new CorpusManifest(CorpusManifest.CurrentGeneratorVersion, Seed: 42, Scale: 10_000, Profile: "default");

        _host = await CorpusHost.StartAsync("pyro-perf-ci", memoryLimitMegabytes: 2048);
        await _host.ApplyMigrationsAsync();
        _host.StartHost();

        if (await _host.ReadStoredManifestAsync() is null)
        {
            var writer = new BulkIndexWriter(_host.ConnectionString, _host.Services);
            await writer.WriteAsync(
                CorpusGenerator.Generate(_manifest, DistributionProfile.Default()),
                batchSize: 1000,
                CancellationToken.None);
            await _host.WriteStoredManifestAsync(_manifest);
        }
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task EveryQueryInTheSetExecutesWithoutError()
    {
        // The cheapest possible regression net: a change that breaks a search pattern outright
        // fails here in seconds, long before anyone runs the 250k corpus.
        var measurer = new QueryMeasurer(_host);
        var failures = new List<string>();

        foreach (QueryDefinition query in QuerySetLoader.Load(QuerySetLoader.DefaultPath))
        {
            try
            {
                await measurer.MeasureAsync(query, iterations: 2, cold: false);
            }
            catch (Exception exception)
            {
                failures.Add($"{query.Name}: {exception.Message}");
            }
        }

        Assert.Empty(failures);
    }

    [Theory]
    [InlineData("string-exact-hot")]
    [InlineData("token-code-hot")]
    [InlineData("reference-direct-hot")]
    public async Task SelectiveQueriesSeekRatherThanScan(string queryName)
    {
        QueryDefinition query = QuerySetLoader.Load(QuerySetLoader.DefaultPath)
            .Single(x => string.Equals(x.Name, queryName, StringComparison.Ordinal));

        var measurer = new QueryMeasurer(_host);
        BaselineEntry entry = await measurer.MeasureAsync(query, iterations: 2, cold: false);

        Assert.Contains(entry.PlanOperators, op => op.Contains("Seek", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheGateCompletesInsideItsMeasuredBudget()
    {
        Assert.True(WallClockBudgetSeconds > 0, "the budget must be set from a measured run (spec §17.1)");

        var stopwatch = Stopwatch.StartNew();
        var measurer = new QueryMeasurer(_host);

        foreach (QueryDefinition query in QuerySetLoader.Load(QuerySetLoader.DefaultPath))
        {
            await measurer.MeasureAsync(query, iterations: 2, cold: false);
        }

        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed.TotalSeconds < WallClockBudgetSeconds,
            $"the gate took {stopwatch.Elapsed.TotalSeconds:N0}s against a budget of {WallClockBudgetSeconds}s");
    }
}
```

- [ ] **Step 2: Run the gate and read the real timing**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter CiGateTest`
Expected: `TheGateCompletesInsideItsMeasuredBudget` FAILS on its first assertion, because the budget is still 0. Every other test should pass.

Note the wall-clock time the test run reports.

- [ ] **Step 3: Set the budget from the measurement**

Replace `WallClockBudgetSeconds = 0` with the measured seconds **doubled and rounded up to the nearest 30**, so ordinary runner variance does not cause a flake. Add a comment recording the measured figure and the date:

```csharp
    /// <summary>
    /// Set from a measured run on 2026-09-29: the full gate took Ns locally. The budget is 2x
    /// that, rounded up, because a GitHub runner is slower and noisier than a dev machine.
    /// Spec §17.1 required this be measured rather than guessed.
    /// </summary>
    private const int WallClockBudgetSeconds = 0; // <- the rounded figure
```

- [ ] **Step 4: Re-run the gate**

Run: `dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter CiGateTest`
Expected: PASS — all five tests.

If `SelectiveQueriesSeekRatherThanScan` fails today, that is a **real finding, not a broken test**: it means the query genuinely scans on the current index set. Record it in `assets/perf/measured-sizes.md` under "Known scans at baseline" and change the assertion to `Assert.Contains(entry.PlanOperators, op => op.Contains("Scan"))` with a comment naming it as the pre-D state and a note that sub-project D flips it. Do **not** delete the test — it is the before-and-after that proves D worked.

- [ ] **Step 5: Wire the gate into CI**

The CI workflow already runs `dotnet test src/Abm.Pyro.CI.slnf`, and Task 1 added both new projects to the filter, so the gate runs automatically with no workflow change. Confirm this by reading `.github/workflows/ci.yml` and checking the test step targets the filter rather than naming projects individually.

If the step names projects individually, add `src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj` to it.

- [ ] **Step 6: Commit**

```bash
git add src/Abm.Pyro.Performance.Test/Integration/CiGateTest.cs assets/perf/measured-sizes.md .github/workflows/ci.yml
git commit -m "test: 10k CI gate asserting seek-not-scan within a measured budget

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

**Note:** pushing a change to `.github/workflows/*` needs a git credential carrying the `workflow` OAuth scope (`CLAUDE.md`). If the push is rejected, run `gh auth setup-git` with a token scoped `repo,workflow,read:org`.

---

### Task 14: `snapshot` export and import, and the documentation

`snapshot` is optional in the spec (§7) and writes outside the repo (§5.3). It exists so a 250k corpus survives a container rebuild without a 3-minute reseed.

**Files:**
- Create: `src/Abm.Pyro.Performance/Cli/SnapshotCommand.cs`
- Modify: `src/Abm.Pyro.Performance/Program.cs`
- Create: `src/Abm.Pyro.Performance/README.md`
- Modify: `CLAUDE.md`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: `CorpusHost` (Task 4), `CorpusManifest` (Task 1).
- Produces: nothing.

- [ ] **Step 1: Implement the `snapshot` command**

`src/Abm.Pyro.Performance/Cli/SnapshotCommand.cs`:

```csharp
using System.CommandLine;
using Abm.Pyro.Performance.Generation;
using Abm.Pyro.Performance.Hosting;
using Microsoft.Data.SqlClient;

namespace Abm.Pyro.Performance.Cli;

/// <summary>
/// BACKUP/RESTORE of the seeded corpus, so a container rebuild does not cost a full reseed.
/// The .bak is written inside the container and copied to a path outside the repository tree
/// (spec §5.3) — a file git cannot reach from the repo root cannot be committed by accident.
/// </summary>
public static class SnapshotCommand
{
    public static Command Create()
    {
        var containerOption = new Option<string>("--container", () => SeedCommand.DefaultContainerName, "Docker container name");
        var pathOption = new Option<string>("--path", CorpusManifest.DefaultCachePath, "Directory for the .bak, outside the repo");

        var export = new Command("export", "Back the seeded corpus up to a .bak");
        export.AddOption(containerOption);
        export.AddOption(pathOption);
        export.SetHandler(async (container, path) =>
        {
            Directory.CreateDirectory(path);

            await using CorpusHost host = await CorpusHost.StartAsync(container, memoryLimitMegabytes: 4096);
            CorpusManifest? manifest = await host.ReadStoredManifestAsync();

            if (manifest is null)
            {
                Console.Error.WriteLine("Nothing to export: this container holds no seeded corpus.");
                Environment.ExitCode = 1;
                return;
            }

            string containerPath = $"/var/opt/mssql/backup/{manifest.ContentHash[..12]}.bak";
            await ExecuteAsync(host.ConnectionString,
                $"BACKUP DATABASE [master] TO DISK = '{containerPath}' WITH INIT, COMPRESSION;");

            Console.WriteLine($"Backup written inside the container at {containerPath}.");
            Console.WriteLine($"Copy it out with:  docker cp {container}:{containerPath} \"{path}\"");
        }, containerOption, pathOption);

        var import = new Command("import", "Restore a corpus .bak into the container");
        import.AddOption(containerOption);
        import.AddOption(pathOption);
        import.SetHandler((container, path) =>
        {
            Console.WriteLine("Copy the .bak into the container, then restore it:");
            Console.WriteLine($"  docker cp \"{path}/<hash>.bak\" {container}:/var/opt/mssql/backup/");
            Console.WriteLine($"  Then run RESTORE DATABASE against that path.");
            Console.WriteLine("Import is deliberately manual: an automatic restore over a populated container");
            Console.WriteLine("would silently replace the corpus a baseline was taken against.");
        }, containerOption, pathOption);

        var command = new Command("snapshot", "Export or import the seeded corpus");
        command.AddCommand(export);
        command.AddCommand(import);

        return command;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 1800;
        await command.ExecuteNonQueryAsync();
    }
}
```

Add to `Program.cs`:

```csharp
        root.AddCommand(Cli.IngestCommand.Create());
        root.AddCommand(Cli.SnapshotCommand.Create());
```

- [ ] **Step 2: Add the belt-and-braces gitignore entries**

Append to `.gitignore`:

```gitignore
# Performance harness — corpora and backups live outside the repo tree by default
# (see CorpusManifest.DefaultCachePath). These entries are the second line of defence.
*.bak
perf-corpus/
```

- [ ] **Step 3: Write the harness README**

`src/Abm.Pyro.Performance/README.md`:

```markdown
# Abm.Pyro.Performance

The FHIR search measurement harness (sub-project A of the search performance programme).
Design: `docs/superpowers/specs/2026-09-27-fhir-search-perf-harness-design.md`, Part II.

It changes no production behaviour. It touches no index, no predicate, no handler.

## Commands

```bash
# Seed a 250k corpus and print measured per-table sizes
dotnet run --project src/Abm.Pyro.Performance -- seed --scale 250000 --report

# Measure the whole query set and write a baseline
dotnet run --project src/Abm.Pyro.Performance -- run --queries all --baseline pre-phase-b

# Diff two baselines
dotnet run --project src/Abm.Pyro.Performance -- compare --from pre-phase-b --to post-phase-b

# Measure write throughput through the real create handler
dotnet run --project src/Abm.Pyro.Performance -- ingest --count 5000

# Back the seeded corpus up so a container rebuild does not cost a reseed
dotnet run --project src/Abm.Pyro.Performance -- snapshot export
```

Docker must be running. The container (`pyro-perf-corpus`) is long-lived and reused between
runs; remove it with `docker rm -f pyro-perf-corpus`.

## What it measures, and what it refuses to

- **Logical reads** per table are the primary metric — hardware-independent and exactly
  reproducible between runs against an unchanged corpus.
- **Plan operators** make "seek, not scan, on index X" a mechanical assertion.
- **Result cardinality** is recorded for every query, so a changed result set is never misread
  as a performance delta.
- It **refuses to run** against a corpus whose hash does not match the one requested.
- It **fails loudly** if plan XML will not parse, rather than recording a defaulted metric.
- It **flags zero-row queries**, whose timings mean nothing.

## The two layers

`QueryRunner` drives the real pipeline and captures the SQL EF emits. That is the evidence of
record. `IndexShapeProbe` runs hand-written SQL against candidate index shapes; it is a
hypothesis generator only, and a shape that wins there still has to win under `QueryRunner`.
```

- [ ] **Step 4: Document the harness in `CLAUDE.md`**

Add a new section to `CLAUDE.md`, after "Integration Tests (`Abm.Pyro.Api.Test`)":

```markdown
## Performance Harness (`Abm.Pyro.Performance`)

A console harness that measures FHIR search against a deterministically generated corpus. It
changes no production behaviour — no index, no predicate, no handler. See
`src/Abm.Pyro.Performance/README.md` for the commands and
`docs/superpowers/specs/2026-09-27-fhir-search-perf-harness-design.md` for the design.

- **Corpus identity.** A corpus is a pure function of (generator version, seed, scale, profile),
  hashed into `CorpusManifest.ContentHash` and stored in a `PerfCorpusManifest` table the
  harness owns. Every command refuses to run against a corpus that is not the one requested.
  `CorpusManifest.CurrentGeneratorVersion` must be bumped by hand whenever a change under
  `Generation/` alters the resources produced — forgetting is the one way to silently
  invalidate a committed baseline.
- **Determinism.** No `DateTime.UtcNow`, no `Guid.NewGuid()`, no unseeded `Random` anywhere
  under `Generation/`. Dates are measured back from `DistributionProfile.AnchorUtc`.
- **Container lifetime.** The harness's SQL container (`pyro-perf-corpus`) is long-lived and
  reused between runs, unlike `IntegrationTestFixture`'s disposable one. Buffer pressure is a
  container memory cap (`--memory-mb`), not a data volume.
- **The real pipeline, not a copy.** `PerformanceWebApplicationFactory` hosts the production DI
  graph via `WebApplicationFactory<Program>`. `BulkIndexWriter` runs the production `IIndexer`.
  `QueryRunner` drives `ISearchQueryService` then `IResourceStoreSearch`. Nothing is
  reimplemented.
- **Tenant must be set by hand.** `PyroDbContext` is built from
  `ITenantService.GetScopedTenant()`, which outside an HTTP request is unset. Resolve services
  through `TenantScope.Create(services)`, never `services.CreateScope()` directly.
- **`IndexPosition` is written via EF, not `SqlBulkCopy`** — `SqlBulkCopy` cannot bind a
  NetTopologySuite `Point` to a `geography` column without `Microsoft.SqlServer.Types`. It is
  the smallest index table, so the cost is seconds.
- **`ResourceStore.Json` is compressed by hand** in the bulk path, because `SqlBulkCopy`
  bypasses EF value converters. `ResourceStoreJsonCompressionConversion.Zip` is `public static`
  for this.
- **Baselines live in `assets/perf/baselines/`** and are committed. They are the evidence every
  later claim in the performance programme rests on. Corpora and `.bak` files are **not**
  committed: they default to `%LOCALAPPDATA%\Pyro\perf-corpus\`, outside the repo tree.
- **The 10k CI gate** runs inside `dotnet test` via the solution filter and asserts plan shape
  (seek, not scan), never wall-clock thresholds.
- **`BulkIndexWriter` is the seam sub-project C (re-index) will generalise** — "run the real
  setters over a resource and write index rows in bulk" is what re-indexing does.
```

- [ ] **Step 5: Build and run everything**

Run: `dotnet build src/Abm.Pyro.CI.slnf`
Expected: succeeds with no new warnings.

Run: `dotnet test src/Abm.Pyro.CI.slnf`
Expected: all six test projects pass — Domain.Test, Application.Test, Repository.Test, Api.Test, and Performance.Test. Docker must be running.

- [ ] **Step 6: Commit**

```bash
git add src/Abm.Pyro.Performance CLAUDE.md .gitignore
git commit -m "feat: snapshot command, harness README and CLAUDE.md documentation

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 15: Verification sweep

The success criteria of spec §16 are claims about the finished harness. This task proves each one with a command and its output, rather than asserting it.

**Files:**
- Modify: `assets/perf/measured-sizes.md`

**Interfaces:**
- Consumes: everything.
- Produces: nothing.

- [ ] **Step 1: §16.1 — seeding completes in roughly three minutes with measured sizes**

```bash
docker rm -f pyro-perf-corpus
dotnet run --project src/Abm.Pyro.Performance -- seed --scale 250000 --report
```

Expected: completes in roughly 3 minutes and prints per-table sizes. Record the wall-clock time in `assets/perf/measured-sizes.md`. If it takes materially longer, raise `batchSize` in `SeedCommand` before accepting the figure.

- [ ] **Step 2: §16.2 — a baseline covering every pattern, with all four fields**

```bash
dotnet run --project src/Abm.Pyro.Performance -- run --queries all --baseline pre-phase-b
```

Then verify every entry carries all four required fields:

```bash
python -c "
import json
b = json.load(open('assets/perf/baselines/pre-phase-b.json'))
for e in b['Entries']:
    assert e['LogicalReadsByTable'], e['Name']
    assert e['PlanOperators'], e['Name']
    assert e['ElapsedMedianMs'] is not None, e['Name']
    assert e['ResultCardinality'] is not None, e['Name']
print(f'{len(b[\"Entries\"])} entries, all four fields present')
"
```

- [ ] **Step 3: §16.3 — `compare` produces a readable delta**

```bash
dotnet run --project src/Abm.Pyro.Performance -- run --queries all --baseline pre-phase-b-check
dotnet run --project src/Abm.Pyro.Performance -- compare --from pre-phase-b --to pre-phase-b-check
```

Expected: a markdown table. Since nothing changed between the two runs, every read delta should be 0% and no cardinality should have changed. Delete `pre-phase-b-check.json` afterwards.

- [ ] **Step 4: §16.4 — logical reads reproduce exactly**

This was verified in Task 9 Step 7. Re-confirm it here against the final 250k corpus, since Task 9 may have run at a smaller scale.

- [ ] **Step 5: §16.5 — a hash mismatch refuses to run**

```bash
dotnet run --project src/Abm.Pyro.Performance -- run --queries string-prefix-hot --baseline throwaway --scale 10000
```

Expected: **fails** with "Corpus hash mismatch", naming both hashes and printing the reseed command. The container holds a 250k corpus, so a 10k request must be refused. Nothing should be written to `assets/perf/baselines/`.

- [ ] **Step 6: §16.6 — the CI gate passes inside its budget**

```bash
dotnet test src/Abm.Pyro.Performance.Test/Abm.Pyro.Performance.Test.csproj --filter CiGateTest
```

Expected: PASS.

- [ ] **Step 7: §16.7 — the index-direction question is answerable**

Open `assets/perf/baselines/pre-phase-b.json` and compare the `PlanOperators` of `token-code-hot` against `token-code-cold`, and `string-prefix-hot` against `string-prefix-cold`.

The spec's criterion is that hot and cold variants of the same parameter show **measurably different plan choices**. Record what you find in `assets/perf/measured-sizes.md` under a "Index direction, first evidence" heading — including if they do *not* differ, because that is itself the answer sub-project D needs and is far more useful recorded than discovered again later.

- [ ] **Step 8: Full build and test**

Run: `dotnet build src/Abm.Pyro.CI.slnf`
Expected: succeeds with no warnings introduced by this work.

Run: `dotnet test src/Abm.Pyro.CI.slnf`
Expected: every test project passes.

- [ ] **Step 9: Confirm no production file was changed**

```bash
git diff --stat main...HEAD -- src/Abm.Pyro.Api src/Abm.Pyro.Application src/Abm.Pyro.Domain src/Abm.Pyro.Repository
```

Expected: **empty**. Sub-project A changes no production behaviour (spec §6, §15). The only permitted entries are `src/Abm.Pyro.CI.slnf` and `src/Abm.Pyro.sln`, which are not under those paths. Any other file listed here is scope that belongs to B2, C, D or E and must be reverted.

- [ ] **Step 10: Commit the evidence**

```bash
git add assets/perf
git commit -m "docs: measured evidence for the harness success criteria

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Notes for whoever executes this

**Read the spec alongside the plan.** Part II of
`docs/superpowers/specs/2026-09-27-fhir-search-perf-harness-design.md` carries reasoning this plan
compresses — particularly §10.1 on why cardinality is a first-class field, and §13 on why silent
wrongness is the enemy.

**Three places where the plan tells you to read production code rather than trust it:**

1. Task 5, Step 3 — the four elided `WriteXxxIndexAsync` methods. Read each `IndexXxx.cs` model
   and mirror every property. `IndexQuantity.CodeHigh`, `SystemHigh`, `UnitHigh` and
   `IndexReference.CanonicalVersionId` are written and never read (spec §2.2f), and they must
   still be written here, because the corpus has to reflect today's ingest cost.
2. Task 6, Step 4 — `ResourceStoreSearchOutcome`'s property names.
3. Task 11, Steps 1 and 3 — the real create-handler interface and `FhirCreateRequest` shape.

**What this sub-project does not do.** No index change, no predicate change, no handler change,
no migration, no `_include` optimisation, no multi-tenant measurement, no measurement against
Azure. Those are B2, C, D and E (spec §15). If a task seems to want one, the task is wrong — stop
and say so rather than reaching into production code.

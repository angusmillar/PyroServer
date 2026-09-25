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

    // Roughly 0.90 km north-west of the Opera House
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
        Assert.InRange(distance.Value.Value, 0.88m, 0.92m);
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
        Assert.NotNull(distance.Value);
        // 0.900 km is ~0.559 mi.
        Assert.InRange(distance.Value.Value, 0.54m, 0.58m);
    }

    [Fact]
    public async Task Search_NearMultiplePositions_ReportsTheDistanceToTheClosest()
    {
        await CreateLocationAsync("Nearby", NearbyLatitude, NearbyLongitude);

        // The far position is deliberately listed first: an implementation that took the first
        // candidate instead of the smallest would report thousands of km and fail this test;
        // only genuine min-of-many logic reports the near position's 0.900 km and passes.
        Bundle? bundle = await FhirClient.SearchAsync<Location>(
            new[] { "near=0|0|5|km,-33.8568|151.2153|5|km" });

        Assert.NotNull(bundle);
        Extension? extension = Assert.Single(bundle.Entry).Search.GetExtension(LocationDistanceUrl);
        Assert.NotNull(extension);

        var distance = Assert.IsType<Distance>(extension.Value);
        Assert.NotNull(distance.Value);
        Assert.InRange(distance.Value.Value, 0.88m, 0.92m);
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

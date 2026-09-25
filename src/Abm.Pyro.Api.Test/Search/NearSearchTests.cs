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
        Assert.NotNull(created.Position);

        created.Position.LatitudeElement = new FhirDecimal(MelbourneLatitude);
        created.Position.LongitudeElement = new FhirDecimal(MelbourneLongitude);

        Location? updated = await FhirClient.UpdateAsync(created);

        Assert.NotNull(updated);
        Assert.NotNull(updated.Position);
        Assert.Equal(MelbourneLatitude, updated.Position.Latitude);
    }

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
        Location location = Assert.IsType<Location>(entry.Resource);
        Assert.Equal("Opera House", location.Name);
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
        Assert.NotNull(created.Position);

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
            new[] { "near=-33.8568|151.2153|1000|km" });

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
        Location location = Assert.IsType<Location>(entry.Resource);
        Assert.Equal("Nowhere", location.Name);
    }

    [Fact]
    public async Task Search_NearMissingFalse_ReturnsLocationsWithAPosition()
    {
        await CreateLocationAsync("Opera House", SydneyLatitude, SydneyLongitude);
        Location? noPosition = await FhirClient.CreateAsync(LocationBuilder.Build(name: "Nowhere"));
        Assert.NotNull(noPosition);

        Bundle? bundle = await FhirClient.SearchAsync<Location>(new[] { "near:missing=false" });

        Assert.NotNull(bundle);
        Bundle.EntryComponent entry = Assert.Single(bundle.Entry);
        Location location = Assert.IsType<Location>(entry.Resource);
        Assert.Equal("Opera House", location.Name);
    }

    /// <summary>
    /// Comma-separated values are an OR in FHIR search, so "missing is true OR missing is false"
    /// matches everything. This pins the fix for a defect where these composed with AND, yielding
    /// an always-false predicate that silently returned nothing.
    /// </summary>
    [Fact]
    public async Task Search_NearMissingTrueAndFalse_ReturnsAllLocations()
    {
        await CreateLocationAsync("Opera House", SydneyLatitude, SydneyLongitude);
        Location? noPosition = await FhirClient.CreateAsync(LocationBuilder.Build(name: "Nowhere"));
        Assert.NotNull(noPosition);

        Bundle? bundle = await FhirClient.SearchAsync<Location>(new[] { "near:missing=true,false" });

        Assert.NotNull(bundle);
        Assert.Equal(2, bundle.Entry.Count);
        List<string?> names = bundle.Entry
            .Select(entry => Assert.IsType<Location>(entry.Resource).Name)
            .ToList();
        Assert.Contains("Opera House", names);
        Assert.Contains("Nowhere", names);
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
}

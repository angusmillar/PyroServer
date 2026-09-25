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
}

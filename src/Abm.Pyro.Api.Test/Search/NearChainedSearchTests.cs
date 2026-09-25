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
        Bundle.EntryComponent entry = Assert.Single(bundle.Entry);
        Assert.NotNull(entry.Search);
        Assert.Null(entry.Search.GetExtension(LocationDistanceUrl));
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
        Assert.IsType<Location>(entry.Resource);
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

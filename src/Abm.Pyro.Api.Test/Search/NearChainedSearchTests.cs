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

    /// <summary>
    /// Forward chain through Location.partOf: returns Locations whose partOf TARGET (the
    /// parent) matches near=X. The child ("ward") carries no position of its own; only its
    /// parent's position is examined.
    /// </summary>
    [Fact]
    public async Task Search_ChainedPartOfNear_ReturnsTheChildLocation()
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

    /// <summary>
    /// Reverse chain (_has) through HasPredicateFactory: returns Locations L such that some
    /// other Location C references L via C.partOf AND C matches near=X. The position lives on
    /// the CHILD ("ward"); the PARENT ("campus"), which carries no position of its own, is what
    /// is returned. This is the mirror image of the forward-chain test above and exercises the
    /// separate _has:-syntax call site distinct from dotted chaining.
    /// </summary>
    [Fact]
    public async Task Search_HasLocationPartOfNear_ReturnsTheParentLocation()
    {
        Location sydneyCampus = await CreateLocationWithoutPositionAsync("Sydney campus");
        Location melbourneCampus = await CreateLocationWithoutPositionAsync("Melbourne campus");

        await CreateChildLocationWithPositionAsync(
            "Sydney ward", SydneyLatitude, SydneyLongitude, parent: sydneyCampus);
        await CreateChildLocationWithPositionAsync(
            "Melbourne ward", MelbourneLatitude, MelbourneLongitude, parent: melbourneCampus);

        // Locations that are referenced (via partof) by a child Location near Sydney.
        Bundle? bundle = await FhirClient.SearchAsync<Location>(
            new[] { "_has:Location:partof:near=-33.8568|151.2153|5|km" });

        Assert.NotNull(bundle);
        Bundle.EntryComponent entry = Assert.Single(bundle.Entry);
        Assert.IsType<Location>(entry.Resource);
        Assert.Equal("Sydney campus", ((Location)entry.Resource).Name);
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

    private async Task<Location> CreateLocationWithoutPositionAsync(string name)
    {
        Location? location = await FhirClient.CreateAsync(LocationBuilder.Build(name: name));
        Assert.NotNull(location);
        return location;
    }

    private async Task CreateChildLocationWithPositionAsync(
        string name, decimal latitude, decimal longitude, Location parent)
    {
        Location child = LocationBuilder.Build(name: name, latitude: latitude, longitude: longitude);
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

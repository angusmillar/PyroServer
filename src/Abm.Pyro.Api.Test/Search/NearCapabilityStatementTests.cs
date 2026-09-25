using Abm.Pyro.Api.Test.Fixtures;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
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
            .SingleOrDefault(x => x.Type == ResourceType.Location.ToString());

        Assert.NotNull(location);

        CapabilityStatement.SearchParamComponent? near =
            location.SearchParam.SingleOrDefault(x => x.Name == "near");

        Assert.NotNull(near);
        Assert.Equal(SearchParamType.Special, near.Type);
    }
}

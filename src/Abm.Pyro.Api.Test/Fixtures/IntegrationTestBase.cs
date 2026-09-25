using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Rest;

namespace Abm.Pyro.Api.Test.Fixtures;

[Collection(nameof(IntegrationTestCollection))]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly IntegrationTestFixture Fixture;
    protected FhirClient FhirClient { get; private set; } = null!;

    /// <summary>
    /// The server clock, shared by the whole test host. Freeze it to control the
    /// Meta.LastUpdated instant the server stamps on a committed resource. It is
    /// returned to the real clock before every test.
    /// </summary>
    protected TestDateTimeProvider Clock => Fixture.Factory.Clock;

    protected IntegrationTestBase(IntegrationTestFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await Fixture.ResetDatabaseAsync();

        Clock.Unfreeze();
        
        var fhirClientSettings = new FhirClientSettings()
        {
            PreferredFormat = ResourceFormat.Json,
            PreferredParameterHandling = SearchParameterHandling.Strict
        };
        
        var tenantBaseAddress = new Uri(Fixture.HttpClient.BaseAddress!, "pyro/");
        FhirClient = new FhirClient(tenantBaseAddress, Fixture.HttpClient, fhirClientSettings);
        
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

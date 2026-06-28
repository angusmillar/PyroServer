namespace Abm.Pyro.Api.Test.Fixtures;

[CollectionDefinition(nameof(IntegrationTestCollection))]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
    // Marker class only. xUnit wires the fixture automatically.
}

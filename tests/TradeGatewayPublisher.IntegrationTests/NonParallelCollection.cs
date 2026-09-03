namespace TradeGatewayPublisher.IntegrationTests
{
    // One IntegrationTestWebApplicationFactory (one app host) is shared across every test in this
    // collection via IntegrationTestFixture. Building the host more than once per process crashes
    // with "Mechanism named 'MONGODB-AWS' already registered" - the Mongo driver's AWS auth
    // mechanism is registered in a process-wide static registry and can only be added once.
    [CollectionDefinition(NonParallelCollection.Name, DisableParallelization = true)]
    public class NonParallelCollection : ICollectionFixture<IntegrationTestFixture>
    {
        public const string Name = "NonParallel";
    }
}

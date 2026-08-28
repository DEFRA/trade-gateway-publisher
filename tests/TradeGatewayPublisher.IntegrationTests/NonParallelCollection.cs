namespace TradeGatewayPublisher.IntegrationTests
{
    [CollectionDefinition(NonParallelCollection.Name, DisableParallelization = true)]
    public static class NonParallelCollection
    {
        public const string Name = "NonParallel";
    }
}

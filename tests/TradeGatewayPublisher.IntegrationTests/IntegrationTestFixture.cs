using Amazon.SQS;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using MongoDB.Driver;

namespace TradeGatewayPublisher.IntegrationTests
{
    public class IntegrationTestFixture : IDisposable, IAsyncLifetime
    {
        public readonly IntegrationTestWebApplicationFactory Factory = new();

        private IConfiguration configuration => Factory.Services.GetRequiredService<IConfiguration>();

        public const string TestSqsQueueName = "trade_gateway_publisher_intra_updates_test.fifo";
        public const string TestAsbSubsName = "trade-gateway-publisher-intra-test-sub";

        public IAmazonSQS AmazonSqs { get; private set; } = null!;

        public string QueueUrl { get; private set; } = null!;
        
        public bool ServiceBusIsEnabled { get; private set; }

        private bool _disposed;

        private HttpClient? _client;

        public async ValueTask InitializeAsync()
        {
            AmazonSqs = Factory.Services.GetRequiredService<IAmazonSQS>();
            QueueUrl = (await AmazonSqs.GetQueueUrlAsync(TestSqsQueueName)).QueueUrl;

            ServiceBusIsEnabled = configuration.GetValue<bool>("FeatureManagement:AzureServiceBusPublishing");
            _client = Factory.CreateClient();
        }

        public async Task DeleteDatabaseAsync()
        {
            var mongoUri = configuration.GetValue<string>("Mongo:DatabaseUri");
            var mongoDbName = configuration.GetValue<string>("Mongo:DatabaseName");
            using var client = new MongoClient(mongoUri);
            await client.DropDatabaseAsync(mongoDbName);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                // Prefer async disposal of the factory
                await Factory.DisposeAsync();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // dispose managed state
                try
                {
                    _client?.Dispose();
                    Factory?.Dispose();
                }
                catch
                {
                    // swallow to avoid throwing from Dispose
                }
            }

            _disposed = true;
        }

        ~IntegrationTestFixture()
        {
            Dispose(disposing: false);
        }
    }
}
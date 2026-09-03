using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using MongoDB.Driver.Core.Misc;
using Xunit.v3;

namespace TradeGatewayPublisher.IntegrationTests
{
    public class IntegrationTestFixture : IDisposable, IAsyncLifetime
    {
        public readonly IntegrationTestWebApplicationFactory Factory = new();

        private IConfiguration configuration => Factory.Services.GetRequiredService<IConfiguration>();

        public const string TestIntraSqsQueueName = "trade_gateway_publisher_intra_updates_test.fifo";
        public const string TestChedSqsQueueName = "trade_gateway_publisher_ched_updates_test.fifo";

        public IAmazonSQS AmazonSqs { get; private set; } = null!;

        public string TestIntraQueueUrl { get; private set; } = null!;
        public string TestChedQueueUrl { get; private set; } = null!;

        public bool ServiceBusIsEnabled { get; private set; }

        private bool _disposed;

        private HttpClient? _client;

        public async ValueTask InitializeAsync()
        {
            AmazonSqs = Factory.Services.GetRequiredService<IAmazonSQS>();
            TestIntraQueueUrl = (await AmazonSqs.GetQueueUrlAsync(TestIntraSqsQueueName)).QueueUrl;
            TestChedQueueUrl = (await AmazonSqs.GetQueueUrlAsync(TestChedSqsQueueName)).QueueUrl;
            ServiceBusIsEnabled = configuration.GetValue<bool>("FeatureManagement:AzureServiceBusPublishing");
        }

        public void StartClient()
        {
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

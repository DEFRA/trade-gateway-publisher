using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeGatewayPublisher.IntegrationTests.IntraChanges;

namespace TradeGatewayPublisher.IntegrationTests
{
    public class IntegrationTestFixture : IDisposable, IAsyncLifetime
    {
        public readonly IntegrationTestWebApplicationFactory Factory;

        public const string TestSqsQueueName = "trade_gateway_publisher_intra_updates_test.fifo";
        public const string TestAsbSubsName = "trade-gateway-publisher-intra-test-sub";

        public IAmazonSQS AmazonSqs { get; private set; } = null!;
        public string QueueUrl { get; private set; } = null!;
        public bool ServiceBusIsEnabled { get; private set; }

        private bool _disposed;

        public IntegrationTestFixture()
        {
            // keep constructor synchronous and minimal; heavy async init happens in InitializeAsync
            Factory = new IntegrationTestWebApplicationFactory();
        }

        public async ValueTask InitializeAsync()
        {
            await WireMockStubber.ResetAsync(Factory.WireMockBaseUrl);

            AmazonSqs = Factory.Services.GetRequiredService<IAmazonSQS>();
            QueueUrl = (await AmazonSqs.GetQueueUrlAsync(TestSqsQueueName)).QueueUrl;

            await AmazonSqs.PurgeQueueAsync(QueueUrl);
            await Task.Delay(TimeSpan.FromSeconds(1));

            var config = Factory.Services.GetRequiredService<IConfiguration>();
            ServiceBusIsEnabled = config.GetValue<bool>("FeatureManagement:AzureServiceBusPublishing");
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

using System.Collections.Concurrent;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Core.Misc;
using NSubstitute;

using TradeGatewayPublisher.IntegrationTests.IntraChanges;
using Serilog;
using Serilog.Extensions.Logging;

namespace TradeGatewayPublisher.IntegrationTests;

public sealed class IntegrationTestWebApplicationFactory(ITestOutputHelper testOutputHelper)
    : WebApplicationFactory<Program>
{
    private const string FlociEndpoint = "http://localhost:4566";
    private const string MongoDatabaseName = "trade-gateway-publisher";
    private const string MongoUri = "mongodb://localhost:27017";
    public string WireMockBaseUrl { get; } = "http://localhost:8088";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration(
            (_, config) =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Scheduler:Jobs:TracesIntraChangesJob:Cron"] = "* * * * * *",

                        // Until the CHED ticket is picked up
                        ["Scheduler:Jobs:TracesChedChangesJob:Disabled"] = "true",

                        // The tests run against a local Mongo running via Docker
                        ["Mongo:DatabaseUri"] = MongoUri,
                        ["Mongo:DatabaseName"] = MongoDatabaseName,

                        // The tests run against a local Floci AWS emulator running via Docker
                        ["USE_FLOCI"] = "true",
                        ["AWS_ACCESS_KEY_ID"] = "test",
                        ["AWS_SECRET_ACCESS_KEY"] = "test",
                        ["AWS_REGION"] = "eu-west-2",
                        ["SNS_ENDPOINT"] = FlociEndpoint,
                        ["SQS_ENDPOINT"] = FlociEndpoint,

                        // These are the topics and queues configured in ./compose/floci/ready.d/10.setup.sh
                        ["TracesUpdatePublisher:IntraInternalTopicArn"] =
                            "arn:aws:sns:eu-west-2:000000000000:trade_gateway_publisher_intra_stream_internal.fifo",
                        ["TracesUpdatePublisher:IntraTopicArn"] =
                            "arn:aws:sns:eu-west-2:000000000000:trade_gateway_publisher_intra_updates.fifo",
                        ["TracesUpdatePublisher:ChedInternalTopicArn"] =
                            "arn:aws:sns:eu-west-2:000000000000:trade_gateway_publisher_ched_stream_internal.fifo",
                        ["TracesUpdatePublisher:ChedTopicArn"] =
                            "arn:aws:sns:eu-west-2:000000000000:trade_gateway_publisher_ched_updates.fifo",

                        ["TracesUpdateConsumer:IntraQueueUrl"] =
                            $"{FlociEndpoint}/000000000000/trade_gateway_publisher_intra_stream_internal_publisher.fifo",
                        ["TracesUpdateConsumer:ChedQueueUrl"] =
                            $"{FlociEndpoint}/000000000000/trade_gateway_publisher_ched_stream_internal_publisher.fifo",

                        ["TracesUpdateConsumer:IntraQueueUrlForAsb"] =
                            $"{FlociEndpoint}/000000000000/trade_gateway_publisher_intra_stream_internal_asb_publisher.fifo",
                        ["TracesUpdateConsumer:ChedQueueUrlForAsb"] =
                            $"{FlociEndpoint}/000000000000/trade_gateway_publisher_ched_stream_internal_asb_publisher.fifo",

                        // Traces Service Bus (development emulator) - required for ASB publisher/consumer options
                        ["TracesServiceBus:Ched:TopicName"] = "trade-gateway-publisher-ched",
                        ["TracesServiceBus:Ched:ConnectionString"] =
                            "Endpoint=sb://localhost:5672;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
                        ["TracesServiceBus:Intra:TopicName"] = "trade-gateway-publisher-intra",
                        ["TracesServiceBus:Intra:ConnectionString"] =
                            "Endpoint=sb://localhost:5672;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",

                        // The tests run against a local WireMock container emulating the Traces Gateway
                        ["TracesGateway:BaseUrl"] = WireMockBaseUrl,

                        // Enable debug logging for tests to capture startup/runtime details
                        ["Serilog:MinimumLevel:Default"] = "Debug",
                    }
                );
            }
        );

        builder.ConfigureServices(services =>
        {
            // Clean Mongo in-case the tests are re-run
            DropDatabase();

            // Floci does not implement the STS GetWebIdentityToken operation that the real
            // StsAuthDelegatingHandler relies on, so stub the STS client to return a fake token.
            // Without this, every Traces Gateway request throws before it is sent.
            var sts = Substitute.For<IAmazonSecurityTokenService>();
            sts.GetWebIdentityTokenAsync(Arg.Any<GetWebIdentityTokenRequest>(), Arg.Any<CancellationToken>())
                .Returns(
                    new GetWebIdentityTokenResponse
                    {
                        WebIdentityToken = "integration-test-token",
                        Expiration = DateTime.UtcNow.AddHours(1),
                    }
                );

            services.RemoveAll<IAmazonSecurityTokenService>();
            services.AddSingleton(sts);

            // Route the app's Serilog output (console, as configured, plus the xunit test output
            // helper when one is supplied) so API logs show up next to the test that triggered them.
            services.Replace(
                ServiceDescriptor.Singleton<ILoggerFactory>(sp =>
                {
                    var loggerConfiguration = new LoggerConfiguration()
                        .ReadFrom.Configuration(sp.GetRequiredService<IConfiguration>())
                        .Enrich.FromLogContext();

                    if (testOutputHelper != null)
                        loggerConfiguration.WriteTo.TestOutputHelper(testOutputHelper);

                    return new SerilogLoggerFactory(loggerConfiguration.CreateLogger(), dispose: true);
                })
            );

            // Replace production Mongo registrations that call AddAWSAuthentication (which is process-global)
            // with test-safe equivalents to avoid "Mechanism named 'MONGODB-AWS' already registered" errors
            services.RemoveAll(typeof(IMongoDatabase));
            services.RemoveAll(typeof(IMongoCollection<Infrastructure.Data.Entities.LeaseEntity>));
            services.RemoveAll(typeof(IMongoCollection<Infrastructure.Data.Entities.JobWatermarkEntity>));

            services.AddSingleton<IMongoDatabase>(sp =>
            {
                var settings = MongoClientSettings.FromConnectionString(MongoUri);
                var client = new MongoClient(settings);
                var db = client.GetDatabase(MongoDatabaseName);

                // Attempt to create the leases TTL index as production does; ignore errors in tests
                try
                {
                    var coll = db.GetCollection<Infrastructure.Data.Entities.LeaseEntity>("leases");
                    var indexModel = new CreateIndexModel<Infrastructure.Data.Entities.LeaseEntity>(
                        Builders<Infrastructure.Data.Entities.LeaseEntity>.IndexKeys.Ascending(x => x.ExpiresAt),
                        new CreateIndexOptions { Name = "ExpiresAtTtlIdx", Background = true, ExpireAfter = TimeSpan.FromMinutes(5) }
                    );

                    coll.Indexes.CreateOne(indexModel);
                }
                catch
                {
                    // best-effort in test environment
                }
                return db;
            });

            services.AddSingleton(sp => sp.GetRequiredService<IMongoDatabase>().GetCollection<Infrastructure.Data.Entities.LeaseEntity>("leases"));
            services.AddSingleton(sp => sp.GetRequiredService<IMongoDatabase>().GetCollection<Infrastructure.Data.Entities.JobWatermarkEntity>("job_watermarks"));
        });
    }

    private static void DropDatabase()
    {
        var client = new MongoClient(MongoUri);
        client.DropDatabase(MongoDatabaseName);
    }
}

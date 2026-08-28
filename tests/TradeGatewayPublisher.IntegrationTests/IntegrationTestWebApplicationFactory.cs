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

using System.Collections.Concurrent;

namespace TradeGatewayPublisher.IntegrationTests;

public sealed class IntegrationTestWebApplicationFactory() : WebApplicationFactory<Program>
{
    private const string FlociEndpoint = "http://localhost:4566";
    private const string MongoDatabaseName = "trade-gateway-publisher";
    private const string MongoUri = "mongodb://localhost:27017";
    public string WireMockBaseUrl { get; } = "http://localhost:8088";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Serilog.Debugging.SelfLog.Enable(msg => File.AppendAllText("C:\\dev\\ee\\serilog-selflog.txt", msg));

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

                        ////["ASPNETCORE_ENVIRONMENT"] = "Development",
                        ////["Serilog:MinimumLevel:Default"] = "Verbose",
                        ////["Serilog:MinimumLevel:Override:Microsoft"] = "Information",
                        ////["Serilog:MinimumLevel:Override:System"] = "Information",

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


            services.AddLogging(c => c.AddConsole());
        });
    }

    private static void DropDatabase()
    {
        var client = new MongoClient(MongoUri);
        client.DropDatabase(MongoDatabaseName);
    }
}

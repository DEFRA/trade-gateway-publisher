using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace TradeGatewayPublisher.IntegrationTests;

public sealed class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>
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
                        ["STS_ENDPOINT"] = FlociEndpoint,

                        // These are the topics and queues configured in init-aws.sh
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

                        // The tests run against a local WireMock container emulating the Traces Gateway
                        ["TracesGateway:BaseUrl"] = WireMockBaseUrl,
                    }
                );
            }
        );

        builder.ConfigureServices(_ =>
        {
            // Clean Mongo in-case the tests are re-run
            DropDatabase();
        });
    }

    private static void DropDatabase()
    {
        var client = new MongoClient(MongoUri);
        client.DropDatabase(MongoDatabaseName);
    }
}

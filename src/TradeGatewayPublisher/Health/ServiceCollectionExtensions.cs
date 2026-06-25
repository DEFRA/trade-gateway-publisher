using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TradeGatewayPublisher.Config;

namespace TradeGatewayPublisher.Health;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHealth(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddMongoDb(
                provider => provider.GetRequiredService<IMongoDatabase>(),
                timeout: TimeSpan.FromSeconds(10),
                tags: [WebApplicationExtensions.Extended]
            )
            .AddSns(
                "SNS - Intra Internal ",
                sp => sp.GetRequiredService<IOptions<TracesUpdatePublisherOptions>>().Value.IntraInternalTopicArn,
                tags: [WebApplicationExtensions.Extended],
                timeout: TimeSpan.FromSeconds(10)
            )
            .AddSns(
                "SNS - Intra External ",
                sp => sp.GetRequiredService<IOptions<TracesUpdatePublisherOptions>>().Value.IntraTopicArn,
                tags: [WebApplicationExtensions.Extended],
                timeout: TimeSpan.FromSeconds(10)
            )
            .AddSns(
                "SNS - Ched Internal ",
                sp => sp.GetRequiredService<IOptions<TracesUpdatePublisherOptions>>().Value.ChedInternalTopicArn,
                tags: [WebApplicationExtensions.Extended],
                timeout: TimeSpan.FromSeconds(10)
            )
            .AddSns(
                "SNS - Ched External ",
                sp => sp.GetRequiredService<IOptions<TracesUpdatePublisherOptions>>().Value.ChedTopicArn,
                tags: [WebApplicationExtensions.Extended],
                timeout: TimeSpan.FromSeconds(10)
            )
            .AddSqs(
                "SQS - Intra Internal",
                sp => sp.GetRequiredService<IOptions<TracesUpdateConsumerOptions>>().Value.IntraQueueUrl,
                timeout: TimeSpan.FromSeconds(10),
                tags: [WebApplicationExtensions.Extended]
            )
            .AddSqs(
                "SQS - Ched Internal",
                sp => sp.GetRequiredService<IOptions<TracesUpdateConsumerOptions>>().Value.ChedQueueUrl,
                timeout: TimeSpan.FromSeconds(10),
                tags: [WebApplicationExtensions.Extended]
            )
            .AddTracesGateway(timeout: TimeSpan.FromSeconds(10), tags: [WebApplicationExtensions.Extended]);

        return services;
    }
}

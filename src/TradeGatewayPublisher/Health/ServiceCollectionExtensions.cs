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
                "SNS",
                sp => sp.GetRequiredService<IOptions<TracesUpdatePublisherOptions>>().Value.TopicArn,
                tags: [WebApplicationExtensions.Extended],
                timeout: TimeSpan.FromSeconds(10)
            )
            .AddSqs(
                "SQS",
                sp => sp.GetRequiredService<IOptions<TracesUpdateConsumerOptions>>().Value.QueueUrl,
                timeout: TimeSpan.FromSeconds(10),
                tags: [WebApplicationExtensions.Extended]
            );

        return services;
    }
}

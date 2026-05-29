using Amazon.SQS;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Infrastructure.TracesGateway;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refit;

namespace Infrastructure.Messaging.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<IServiceProvider, string> queueUrlFactory
    )
    {
        services.AddSingleton<ISnsPublisher, SnsPublisher>();

        services.AddHostedService(sp => new SqsConsumerBackgroundService(
            queueUrl: queueUrlFactory(sp),
            sqsClient: sp.GetRequiredService<IAmazonSQS>(),
            consumer: sp.GetRequiredService<IMessageConsumer>(),
            logger: sp.GetRequiredService<ILogger<SqsConsumerBackgroundService>>(),
            middlewares: sp.GetServices<IConsumeMiddleware>()
        ));

        return services;
    }
}

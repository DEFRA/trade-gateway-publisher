using System.Diagnostics.Metrics;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Infrastructure.Messaging.Publishing.Middleware;
using Infrastructure.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Messaging.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<IServiceProvider, string> queueUrlFactory,
        bool useLocalStack = false
    )
    {
        services.AddOptions<LocalStackOptions>().Bind(configuration);
        services.AddSingleton<ISnsPublisher, SnsPublisher>();

        services.AddSingleton<IPublishMiddleware, MetricsPublishMiddleware>();
        services.AddSingleton<IPublishMiddleware, TracingPublishMiddleware>();

        services.AddSingleton<IConsumeMiddleware, TracingConsumeMiddleware>();
        services.AddSingleton<IConsumeMiddleware, MetricsConsumeMiddleware>();
        services.AddSingleton<IConsumeMiddleware, LoggingConsumeMiddleware>();

        services.AddSingleton<ConsumerMetrics>(sp => new ConsumerMetrics(
            sp.GetRequiredService<IMeterFactory>(),
            MetricNames.MeterName
        ));

        services.AddSingleton<PublishMetrics>(sp => new PublishMetrics(
            sp.GetRequiredService<IMeterFactory>(),
            MetricNames.MeterName
        ));

        services.AddHostedService(sp => new SqsConsumerBackgroundService(
            queueUrl: queueUrlFactory(sp),
            sqsClient: sp.GetRequiredService<IAmazonSQS>(),
            consumer: sp.GetRequiredService<IMessageConsumer>(),
            logger: sp.GetRequiredService<ILogger<SqsConsumerBackgroundService>>(),
            middlewares: sp.GetServices<IConsumeMiddleware>()
        ));

        services.AddSingleton<IAmazonSimpleNotificationService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ResilientSnsClient>>();

            var localStackOptions = sp.GetRequiredService<IOptions<LocalStackOptions>>().Value;
            if (localStackOptions.UseLocalStack == false)
                return new ResilientSnsClient(logger);

            return new ResilientSnsClient(
                logger,
                new BasicAWSCredentials(localStackOptions.AccessKeyId, localStackOptions.SecretAccessKey),
                new AmazonSimpleNotificationServiceConfig
                {
                    // https://github.com/aws/aws-sdk-net/issues/1781
                    AuthenticationRegion = localStackOptions.AwsRegion ?? RegionEndpoint.EUWest2.ToString(),
                    RegionEndpoint = RegionEndpoint.GetBySystemName(
                        localStackOptions.AwsRegion ?? RegionEndpoint.EUWest2.ToString()
                    ),
                    ServiceURL = localStackOptions.SnsEndpoint,
                }
            );
        });

        services.AddSingleton<IAmazonSQS>(sp =>
        {
            var localStackOptions = sp.GetRequiredService<IOptions<LocalStackOptions>>().Value;
            if (localStackOptions.UseLocalStack == false)
                return new AmazonSQSClient();

            return new AmazonSQSClient(
                new BasicAWSCredentials(localStackOptions.AccessKeyId, localStackOptions.SecretAccessKey),
                new AmazonSQSConfig
                {
                    // https://github.com/aws/aws-sdk-net/issues/1781
                    AuthenticationRegion = localStackOptions.AwsRegion ?? RegionEndpoint.EUWest2.ToString(),
                    RegionEndpoint = RegionEndpoint.GetBySystemName(
                        localStackOptions.AwsRegion ?? RegionEndpoint.EUWest2.ToString()
                    ),
                    ServiceURL = localStackOptions.SqsEndpoint,
                }
            );
        });

        return services;
    }
}

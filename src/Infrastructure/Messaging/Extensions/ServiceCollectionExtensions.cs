using System.Diagnostics.Metrics;
using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Azure.Messaging.ServiceBus;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Infrastructure.Messaging.Publishing.Middleware;
using Infrastructure.Resilience;
using Microsoft.Extensions.Azure;
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
        bool useFloci = false
    )
    {
        services.AddOptions<FlociOptions>().Bind(configuration);
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

        services.AddSingleton<IAmazonSimpleNotificationService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ResilientSnsClient>>();

            var flociOptions = sp.GetRequiredService<IOptions<FlociOptions>>().Value;
            if (flociOptions.UseFloci == false)
                return new ResilientSnsClient(logger);

            return new ResilientSnsClient(
                logger,
                new BasicAWSCredentials(flociOptions.AccessKeyId, flociOptions.SecretAccessKey),
                new AmazonSimpleNotificationServiceConfig
                {
                    // https://github.com/aws/aws-sdk-net/issues/1781
                    AuthenticationRegion = flociOptions.AwsRegion ?? RegionEndpoint.EUWest2.ToString(),
                    RegionEndpoint = RegionEndpoint.GetBySystemName(
                        flociOptions.AwsRegion ?? RegionEndpoint.EUWest2.ToString()
                    ),
                    ServiceURL = flociOptions.SnsEndpoint,
                }
            );
        });

        services.AddSingleton<IAmazonSQS>(sp =>
        {
            var flociOptions = sp.GetRequiredService<IOptions<FlociOptions>>().Value;
            if (flociOptions.UseFloci == false)
                return new AmazonSQSClient();

            return new AmazonSQSClient(
                new BasicAWSCredentials(flociOptions.AccessKeyId, flociOptions.SecretAccessKey),
                new AmazonSQSConfig
                {
                    // https://github.com/aws/aws-sdk-net/issues/1781
                    AuthenticationRegion = flociOptions.AwsRegion ?? RegionEndpoint.EUWest2.ToString(),
                    RegionEndpoint = RegionEndpoint.GetBySystemName(
                        flociOptions.AwsRegion ?? RegionEndpoint.EUWest2.ToString()
                    ),
                    ServiceURL = flociOptions.SqsEndpoint,
                }
            );
        });

        services.AddOptions<CdpOptions>().Bind(configuration);
        var tracesServiceBusOptions = configuration
            .GetRequiredSection(TracesServiceBusOptions.SectionName)
            .Get<TracesServiceBusOptions>()!;

        services.AddTradeGatewayServiceBus(tracesServiceBusOptions);

        return services;
    }

    public static void AddConsumer<TConsumer>(
        this IServiceCollection services,
        Func<IServiceProvider, string> queueUrlFactory
    )
        where TConsumer : class, IMessageConsumer
    {
        services.AddSingleton<IMessageConsumer, TConsumer>();
        services.AddSingleton<TConsumer>();
        services.AddHostedService(sp => new SqsConsumerBackgroundService<TConsumer>(
            queueUrl: queueUrlFactory(sp),
            sqsClient: sp.GetRequiredService<IAmazonSQS>(),
            consumer: sp.GetRequiredService<TConsumer>(),
            logger: sp.GetRequiredService<ILogger<SqsConsumerBackgroundService<TConsumer>>>(),
            middlewares: sp.GetServices<IConsumeMiddleware>()
        ));
    }

    internal static IServiceCollection AddTradeGatewayServiceBus(
        this IServiceCollection services,
        TracesServiceBusOptions tracesServiceBusOptions
    )
    {
        services.AddSingleton<IAsbPublisher, AsbPublisher>();
        services.AddAzureClients(azureBuilder =>
        {
            ServiceBusQueue[] queues = [tracesServiceBusOptions.Ched, tracesServiceBusOptions.Intra];
            foreach (var queue in queues)
            {
                azureBuilder
                    .AddServiceBusClient(queue.ConnectionString)
                    .WithName(queue.QueueName)
                    .ConfigureOptions(
                        (options, provider) =>
                        {
                            if (provider.GetRequiredService<IOptions<CdpOptions>>().Value.IsProxyEnabled)
                            {
                                options.TransportType = ServiceBusTransportType.AmqpWebSockets;
                                options.WebProxy = provider.GetRequiredService<IWebProxy>();
                            }
                        }
                    );

                azureBuilder
                    .AddClient<ServiceBusSender, ServiceBusClientOptions>(
                        (_, _, provider) =>
                        {
                            var clientFactory = provider.GetRequiredService<IAzureClientFactory<ServiceBusClient>>();
                            var client = clientFactory.CreateClient(queue.QueueName);
                            return client.CreateSender(queue.QueueName);
                        }
                    )
                    .WithName(queue.QueueName);
            }
        });
        return services;
    }
}

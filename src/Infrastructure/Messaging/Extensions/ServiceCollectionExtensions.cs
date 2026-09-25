using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Azure.Core;
using Azure.Messaging.ServiceBus;
using Infrastructure.Messaging.Authentication;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Infrastructure.Messaging.Publishing.Middleware;
using Infrastructure.Resilience;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;

namespace Infrastructure.Messaging.Extensions;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FlociOptions>().Bind(configuration);
        services.AddSingleton<ISnsPublisher, SnsPublisher>();

        services.AddSingleton<IPublishMiddleware, MetricsPublishMiddleware>();
        services.AddSingleton<IPublishMiddleware, TracingPublishMiddleware>();

        services.AddSingleton<IConsumeMiddleware, TracingConsumeMiddleware>();
        services.AddSingleton<IConsumeMiddleware, MetricsConsumeMiddleware>();
        services.AddSingleton<IConsumeMiddleware, LoggingConsumeMiddleware>();

        services.AddFeatureManagement();

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

        services.AddTradeGatewayServiceBus(configuration);

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
        IConfiguration configuration
    )
    {
        if (configuration.FeatureIsEnabled(FeatureFlags.AzureServiceBusPublishing))
        {
            services.AddSingleton<IAsbPublisher, AsbPublisher>();

            var tracesServiceBusOptions = configuration
                .GetRequiredSection(TracesServiceBusOptions.SectionName)
                .Get<TracesServiceBusOptions>()!;

            var useSharedServiceBusKey = configuration.FeatureIsEnabled(FeatureFlags.UseSharedAccessKeyForServiceBus);
            if (!useSharedServiceBusKey)
            {
                // Ensure Entra options are available from the TracesServiceBus configuration
                var entraOpts =
                    tracesServiceBusOptions.EntraOptions
                    ?? throw new InvalidOperationException(
                        "TracesServiceBus:EntraOptions must be configured when using Entra authentication"
                    );

                // Register a named IOptions<EntraOptions> backed by the TracesServiceBus configuration
                services.AddSingleton<IOptions<EntraOptions>>(Options.Create(entraOpts));

                services.AddSingleton<IAmazonSecurityTokenService>(sp => new AmazonSecurityTokenServiceClient());

                services.AddHttpClient();
                // Factory for creating ClientAssertionCredential (used by EntraTokenProvider)
                services.AddSingleton<IClientAssertionCredentialFactory, ClientAssertionCredentialFactory>();
                services.AddSingleton<IEntraTokenProvider, EntraTokenProvider>();
                services.AddSingleton<TokenCredential, EntraTokenCredential>();
            }

            services.AddAzureClients(azureBuilder =>
            {
                ServiceBusTopic[] topics = [tracesServiceBusOptions.Ched, tracesServiceBusOptions.Intra];
                foreach (var topicName in topics.Select(topic => topic.TopicName))
                {
                    azureBuilder
                        .AddClient<ServiceBusClient, ServiceBusClientOptions>(
                            (_, _, provider) =>
                            {
                                var env = provider.GetRequiredService<IHostEnvironment>();

                                // Optionally use the connection string (development only)
                                if (useSharedServiceBusKey)
                                {
                                    if (!env.IsDevelopment())
                                        throw new InvalidOperationException(
                                            "UseSharedAccessKey is only supported for Development environments."
                                        );

                                    if (string.IsNullOrEmpty(tracesServiceBusOptions.ConnectionString))
                                        throw new InvalidOperationException(
                                            "TracesServiceBus:ConnectionString must be configured when using shared access key."
                                        );

                                    return new ServiceBusClient(tracesServiceBusOptions.ConnectionString);
                                }

                                // Use TokenCredential (Entra) in non-dev or when feature disabled
                                var credential = provider.GetRequiredService<TokenCredential>();
                                var clientOptions = new ServiceBusClientOptions();

                                if (provider.GetRequiredService<IOptions<CdpOptions>>().Value.IsProxyEnabled)
                                {
                                    clientOptions.TransportType = ServiceBusTransportType.AmqpWebSockets;
                                    clientOptions.WebProxy = provider.GetRequiredService<IWebProxy>();
                                }

                                var fullyQualifiedNamespace =
                                    tracesServiceBusOptions.EntraOptions?.Namespace
                                    ?? throw new InvalidOperationException(
                                        "Entra namespace must be configured in TracesServiceBus:EntraOptions:Namespace when using Entra authentication"
                                    );

                                return new ServiceBusClient(fullyQualifiedNamespace, credential, clientOptions);
                            }
                        )
                        .WithName(topicName);

                    azureBuilder
                        .AddClient<ServiceBusSender, ServiceBusClientOptions>(
                            (_, _, provider) =>
                            {
                                var clientFactory = provider.GetRequiredService<
                                    IAzureClientFactory<ServiceBusClient>
                                >();
                                var client = clientFactory.CreateClient(topicName);
                                return client.CreateSender(topicName);
                            }
                        )
                        .WithName(topicName);
                }
            });
        }
        else
        {
            services.AddSingleton<IAsbPublisher, NullAsbPublisher>();
        }

        return services;
    }
}

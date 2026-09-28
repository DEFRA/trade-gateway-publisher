using System.Diagnostics.CodeAnalysis;
using System.Net;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using HealthChecks.AzureServiceBus;
using HealthChecks.AzureServiceBus.Configuration;
using Infrastructure;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Extensions;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TradeGatewayPublisher.Utils.Http;

namespace TradeGatewayPublisher.Health;

[ExcludeFromCodeCoverage]
public static class AsbHealthCheckBuilderExtensions
{
    public static IHealthChecksBuilder AddAsbTopic(
        this IHealthChecksBuilder builder,
        string name,
        Func<IServiceProvider, ServiceBusTopic> publisherFunc,
        HealthStatus? failureStatus = HealthStatus.Unhealthy,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null
    )
    {
        builder.Add(
            new HealthCheckRegistration(
                name,
                sp => CreateHealthCheck(sp, publisherFunc(sp)),
                failureStatus,
                tags,
                timeout
            )
        );

        return builder;
    }

    private static AzureServiceBusTopicHealthCheck CreateHealthCheck(
        IServiceProvider serviceProvider,
        ServiceBusTopic subscription
    )
    {
        var options = new AzureServiceBusTopicHealthCheckOptions(subscription.TopicName);

        var provider = new ServiceBusClientProvider(serviceProvider);

        return new AzureServiceBusTopicHealthCheck(options, provider);
    }

    private sealed class ServiceBusClientProvider(IServiceProvider serviceProvider)
        : HealthChecks.AzureServiceBus.ServiceBusClientProvider
    {
        private readonly TracesServiceBusOptions _tracesServiceBusOptions = serviceProvider
            .GetRequiredService<IOptions<TracesServiceBusOptions>>()
            .Value;

        private readonly bool _useSharedServiceBusKey = serviceProvider
            .GetRequiredService<IConfiguration>()
            .FeatureIsEnabled(FeatureFlags.UseSharedAccessKeyForServiceBus);

        private readonly bool _isProxyEnabled = serviceProvider
            .GetRequiredService<IOptions<CdpOptions>>()
            .Value.IsProxyEnabled;

        public override ServiceBusClient CreateClient(string? connectionString)
        {
            var clientOptions = _isProxyEnabled
                ? new ServiceBusClientOptions
                {
                    WebProxy = serviceProvider.GetRequiredService<IWebProxy>(),
                    TransportType = ServiceBusTransportType.AmqpWebSockets,
                }
                : new ServiceBusClientOptions();

            return _useSharedServiceBusKey
                ? new ServiceBusClient(_tracesServiceBusOptions.ConnectionString, clientOptions)
                : new ServiceBusClient(
                    _tracesServiceBusOptions.EntraOptions?.Namespace,
                    GetTokenCredential(),
                    clientOptions
                );
        }

        public override ServiceBusAdministrationClient CreateManagementClient(string? connectionString)
        {
            var clientOptions = _isProxyEnabled
                ? new ServiceBusAdministrationClientOptions()
                : new ServiceBusAdministrationClientOptions
                {
                    Transport = new HttpClientTransport(
                        serviceProvider
                            .GetRequiredService<IHttpClientFactory>()
                            .CreateClient(HttpClientRegistrationExtensions.ProxyClientName)
                    ),
                };

            clientOptions.Retry.MaxRetries = 0;

            return _useSharedServiceBusKey
                ? new ServiceBusAdministrationClient(_tracesServiceBusOptions.ConnectionString, clientOptions)
                : new ServiceBusAdministrationClient(
                    _tracesServiceBusOptions.EntraOptions?.Namespace,
                    GetTokenCredential(),
                    clientOptions
                );
        }

        private TokenCredential GetTokenCredential()
        {
            return serviceProvider.GetRequiredService<TokenCredential>();
        }
    }
}

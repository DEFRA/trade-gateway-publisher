using System.Diagnostics.CodeAnalysis;
using System.Net;
using Azure.Core.Pipeline;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using HealthChecks.AzureServiceBus;
using HealthChecks.AzureServiceBus.Configuration;
using Infrastructure.Messaging;
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
        var options = new AzureServiceBusTopicHealthCheckOptions(subscription.TopicName)
        {
            ConnectionString = subscription.ConnectionString,
        };

        return new AzureServiceBusTopicHealthCheck(options, new ServiceBusClientProvider(serviceProvider));
    }

    private sealed class ServiceBusClientProvider(IServiceProvider serviceProvider)
        : HealthChecks.AzureServiceBus.ServiceBusClientProvider
    {
        public override ServiceBusClient CreateClient(string? connectionString)
        {
            var clientOptions = !serviceProvider.GetRequiredService<IOptions<CdpOptions>>().Value.IsProxyEnabled
                ? new ServiceBusClientOptions()
                : new ServiceBusClientOptions
                {
                    WebProxy = serviceProvider.GetRequiredService<IWebProxy>(),
                    TransportType = ServiceBusTransportType.AmqpWebSockets,
                };

            return new ServiceBusClient(connectionString, clientOptions);
        }

        public override ServiceBusAdministrationClient CreateManagementClient(string? connectionString)
        {
            var clientOptions = !serviceProvider.GetRequiredService<IOptions<CdpOptions>>().Value.IsProxyEnabled
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

            return new ServiceBusAdministrationClient(connectionString, clientOptions);
        }
    }
}

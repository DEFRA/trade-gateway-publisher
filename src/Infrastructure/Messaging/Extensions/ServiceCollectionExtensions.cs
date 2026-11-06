using System.Diagnostics.CodeAnalysis;
using System.Net;
using Amazon.SecurityToken;
using Azure.Core;
using Azure.Messaging.ServiceBus;
using Infrastructure.Messaging.Authentication;
using Infrastructure.Messaging.Publishing;
using Infrastructure.Resilience;
using Infrastructure.Resilience;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;

namespace Infrastructure.Messaging.Extensions;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServiceBusMessaging(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        });

        services.AddOptions<CdpOptions>().Bind(configuration);

        services.AddTradeGatewayServiceBus(configuration);

        return services;
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
                RegisterEntraServices(services, tracesServiceBusOptions);
            }

            services.AddAzureClients(azureBuilder =>
                ConfigureAzureClients(azureBuilder, tracesServiceBusOptions, useSharedServiceBusKey)
            );
        }
        else
        {
            services.AddSingleton<IAsbPublisher, NullAsbPublisher>();
        }

        return services;
    }

    private static void RegisterEntraServices(
        IServiceCollection services,
        TracesServiceBusOptions tracesServiceBusOptions
    )
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

    private static void ConfigureAzureClients(
        AzureClientFactoryBuilder azureBuilder,
        TracesServiceBusOptions tracesServiceBusOptions,
        bool useSharedServiceBusKey
    )
    {
        ServiceBusTopic[] topics =
        [
            tracesServiceBusOptions.Ched,
            tracesServiceBusOptions.Intra,
            tracesServiceBusOptions.ClassificationTreeNode,
        ];
        foreach (var topicName in topics.Select(topic => topic.TopicName))
        {
            azureBuilder
                .AddClient<ServiceBusClient, ServiceBusClientOptions>(
                    (_, _, provider) =>
                        CreateServiceBusClient(provider, tracesServiceBusOptions, useSharedServiceBusKey)
                )
                .WithName(topicName);

            azureBuilder
                .AddClient<ServiceBusSender, ServiceBusClientOptions>(
                    (_, _, provider) =>
                    {
                        var clientFactory = provider.GetRequiredService<IAzureClientFactory<ServiceBusClient>>();
                        var client = clientFactory.CreateClient(topicName);
                        return client.CreateSender(topicName);
                    }
                )
                .WithName(topicName);
        }
    }

    private static ServiceBusClient CreateServiceBusClient(
        IServiceProvider provider,
        TracesServiceBusOptions tracesServiceBusOptions,
        bool useSharedServiceBusKey
    )
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
}

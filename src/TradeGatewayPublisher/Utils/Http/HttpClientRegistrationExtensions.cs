using System.Diagnostics.CodeAnalysis;
using System.Net;
using Infrastructure.Messaging;
using Microsoft.Extensions.Options;

namespace TradeGatewayPublisher.Utils.Http;

[ExcludeFromCodeCoverage]
public static class HttpClientRegistrationExtensions
{
    public const string ProxyClientName = "proxy";

    public static IServiceCollection AddHttpProxyClients(this IServiceCollection services)
    {
        // Register IWebProxy for components (e.g., Azure Service Bus clients) that request it.
        services.AddSingleton<IWebProxy>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CdpOptions>>().Value;
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(ProxyClientName);

            var proxy = new WebProxy { BypassProxyOnLocal = true };
            if (!string.IsNullOrWhiteSpace(options.CdpHttpsProxy))
            {
                logger.LogDebug("Creating proxy http client");
                var uriBuilder = new UriBuilder(options.CdpHttpsProxy);

                var username = uriBuilder.UserName;
                var password = uriBuilder.Password;
                if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                {
                    proxy.Credentials = new NetworkCredential(username, password);
                }

                // Remove credentials from the URI so they don't get logged
                uriBuilder.UserName = "";
                uriBuilder.Password = "";
                proxy.Address = uriBuilder.Uri;
            }
            else
            {
                logger.LogWarning("CDP_HTTPS_PROXY is NOT set, proxy client will be disabled");
            }

            return proxy;
        });

        services.AddTransient<ProxyHttpMessageHandler>();

        // Some .net connections use this http client - notably health-check
        services
            .AddHttpClient(ProxyClientName)
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var options = sp.GetRequiredService<IOptions<CdpOptions>>();
                var proxy = sp.GetRequiredService<IWebProxy>();

                return new HttpClientHandler { Proxy = proxy, UseProxy = options.Value.CdpHttpsProxy != null };
            });

        return services;
    }
}

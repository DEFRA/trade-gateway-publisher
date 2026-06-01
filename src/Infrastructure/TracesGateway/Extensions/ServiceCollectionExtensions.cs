using System.Diagnostics.Metrics;
using Infrastructure.Leasing;
using Infrastructure.Scheduler;
using Infrastructure.Scheduler.Metrics;
using Infrastructure.Watermark;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refit;

namespace Infrastructure.TracesGateway.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTracesGateway(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<TracesGatewayOptions>()
            .Bind(configuration.GetSection(TracesGatewayOptions.SectionName))
            .ValidateOnStart();

        services
            .AddRefitClient<ITracesGateway>()
            .ConfigureHttpClient(
                (sp, c) =>
                {
                    var options = sp.GetRequiredService<IOptions<TracesGatewayOptions>>().Value;
                    c.BaseAddress = new Uri(options.BaseUrl);
                }
            )
            .AddHttpMessageHandler<TracingDelegatingHandler>();

        services.AddSingleton<TracingDelegatingHandler>();
        return services;
    }
}

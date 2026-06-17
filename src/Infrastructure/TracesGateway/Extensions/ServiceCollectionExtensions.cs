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
            .AddRefitClient<ITracesGateway>(provider => new RefitSettings
            {
                UrlParameterFormatter = provider.GetRequiredService<UtcDateTimeUrlParameterFormatter>(),
            })
            .ConfigureHttpClient(
                (sp, c) =>
                {
                    var options = sp.GetRequiredService<IOptions<TracesGatewayOptions>>().Value;
                    c.BaseAddress = new Uri(options.BaseUrl);
                }
            )
            .AddHttpMessageHandler<TracingDelegatingHandler>()
            .AddHttpMessageHandler<AcceptLanguageDelegatingHandle>();

        services.AddSingleton<TracingDelegatingHandler>();
        services.AddSingleton<AcceptLanguageDelegatingHandle>();

        return services;
    }
}

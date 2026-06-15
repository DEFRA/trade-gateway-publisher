using System.Diagnostics.Metrics;
using Amazon;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Infrastructure.Leasing;
using Infrastructure.Messaging;
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

        services.AddSingleton<IAmazonSecurityTokenService>(sp =>
        {
            var localStackOptions = sp.GetRequiredService<IOptions<LocalStackOptions>>().Value;
            if (localStackOptions.UseLocalStack == false)
                return new AmazonSecurityTokenServiceClient();

            return new AmazonSecurityTokenServiceClient(
                new BasicAWSCredentials(localStackOptions.AccessKeyId, localStackOptions.SecretAccessKey),
                new AmazonSecurityTokenServiceConfig
                {
                    AuthenticationRegion = localStackOptions.AwsRegion ?? RegionEndpoint.EUWest2.ToString(),
                    RegionEndpoint = RegionEndpoint.GetBySystemName(
                        localStackOptions.AwsRegion ?? RegionEndpoint.EUWest2.ToString()
                    ),
                    ServiceURL = localStackOptions.StsEndpoint,
                }
            );
        });

        services
            .AddRefitClient<ITracesGateway>()
            .ConfigureHttpClient(
                (sp, c) =>
                {
                    var options = sp.GetRequiredService<IOptions<TracesGatewayOptions>>().Value;
                    c.BaseAddress = new Uri(options.BaseUrl);
                }
            )
            .AddHttpMessageHandler<StsAuthDelegatingHandler>()
            .AddHttpMessageHandler<TracingDelegatingHandler>();

        services.AddSingleton<StsAuthDelegatingHandler>();
        services.AddSingleton<TracingDelegatingHandler>();
        return services;
    }
}

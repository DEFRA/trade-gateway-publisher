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
        services.AddSingleton<UtcDateTimeUrlParameterFormatter>();
        services
            .AddOptions<TracesGatewayOptions>()
            .Bind(configuration.GetSection(TracesGatewayOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IAmazonSecurityTokenService>(sp =>
        {
            var localStackOptions = sp.GetRequiredService<IOptions<FlociOptions>>().Value;
            if (localStackOptions.UseFloci == false)
                return new AmazonSecurityTokenServiceClient();

            var config = new AmazonSecurityTokenServiceConfig
            {
                AuthenticationRegion = localStackOptions.AwsRegion ?? RegionEndpoint.EUWest2.ToString(),
                RegionEndpoint = RegionEndpoint.GetBySystemName(
                    localStackOptions.AwsRegion ?? RegionEndpoint.EUWest2.ToString()
                ),
            };

            // Setting ServiceURL clears RegionEndpoint (they are mutually exclusive in the AWS SDK),
            // so only override it when an STS endpoint is actually configured.
            if (!string.IsNullOrWhiteSpace(localStackOptions.StsEndpoint))
                config.ServiceURL = localStackOptions.StsEndpoint;

            return new AmazonSecurityTokenServiceClient(
                new BasicAWSCredentials(localStackOptions.AccessKeyId, localStackOptions.SecretAccessKey),
                config
            );
        });
		
		services.ConfigureHttpClientDefaults(http =>
        {
            http.RedactLoggedHeaders(_ => false);
        });

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
            .AddHttpMessageHandler<StsAuthDelegatingHandler>()
            .AddHttpMessageHandler<HttpLoggingDelegatingHandler>()
            .AddHttpMessageHandler<TracingDelegatingHandler>()
            .AddHttpMessageHandler<AcceptLanguageDelegatingHandle>();

        services.AddSingleton<StsAuthDelegatingHandler>();
        services.AddSingleton<TracingDelegatingHandler>();
        services.AddSingleton<AcceptLanguageDelegatingHandle>();
        services.AddSingleton<HttpLoggingDelegatingHandler>();

        return services;
    }
}

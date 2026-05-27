using System.Diagnostics.CodeAnalysis;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Infrastructure.Data.Extensions;
using Infrastructure.Resilience;
using Infrastructure.Scheduler;
using Infrastructure.Scheduler.Extensions;
using Microsoft.Extensions.Options;
using Serilog;
using TradeGatewayPublisher.Config;
using TradeGatewayPublisher.Health;
using TradeGatewayPublisher.Jobs;
using TradeGatewayPublisher.Jobs.Middleware;
using TradeGatewayPublisher.Utils;
using TradeGatewayPublisher.Utils.Http;
using TradeGatewayPublisher.Utils.Logging;

var app = BuildApp(args);
await app.RunAsync();

[ExcludeFromCodeCoverage]
static WebApplication BuildApp(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    var integrationTest = args.Contains("--integrationTest=true");
    ConfigureHost(builder);
    ConfigureServices(builder, integrationTest);

    var app = builder.Build();

    ConfigureMiddleware(app);
    ConfigureEndpoints(app);

    return app;
}

[ExcludeFromCodeCoverage]
static void ConfigureHost(WebApplicationBuilder builder)
{
    builder.Host.UseSerilog(CdpLogging.Configuration);
}

[ExcludeFromCodeCoverage]
static void ConfigureServices(WebApplicationBuilder builder, bool integrationTest)
{
    var services = builder.Services;
    var configuration = builder.Configuration;

    // Trust material must be loaded before anything creates outbound connections.
    services.LoadCustomTrustStoreFromEnvironment();

    services.AddProblemDetails();
    services.AddValidation();

    services.AddHttpContextAccessor();

    ConfigureHeaderPropagation(services, configuration);
    ConfigureHttpClients(services);
    ConfigureAppServices(services, configuration, integrationTest);

    services.AddHealth();
}

[ExcludeFromCodeCoverage]
static void ConfigureHeaderPropagation(IServiceCollection services, IConfiguration configuration)
{
    var traceHeader = configuration.GetValue<string>("TraceHeader");

    services.AddHeaderPropagation(options =>
    {
        if (!string.IsNullOrWhiteSpace(traceHeader))
        {
            options.Headers.Add(traceHeader);
        }
    });
}

[ExcludeFromCodeCoverage]
static void ConfigureHttpClients(IServiceCollection services)
{
    services.AddTransient<ProxyHttpMessageHandler>();
}

[ExcludeFromCodeCoverage]
static void ConfigureAppServices(IServiceCollection services, IConfiguration configuration, bool integrationTest)
{
    services.AddOptions<LocalStackOptions>().Bind(configuration);
    services
        .AddOptions<TracesUpdatePublisherOptions>()
        .Bind(configuration.GetSection(TracesUpdatePublisherOptions.SectionName))
        .ValidateOnStart();
    services
        .AddOptions<TracesUpdateConsumerOptions>()
        .Bind(configuration.GetSection(TracesUpdateConsumerOptions.SectionName))
        .ValidateOnStart();
    services.AddSingleton<IAmazonSimpleNotificationService>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<ResilientSnsClient>>();

        var localStackOptions = sp.GetRequiredService<IOptions<LocalStackOptions>>().Value;
        if (localStackOptions.UseLocalStack == false)
            return new ResilientSnsClient(logger);

        return new ResilientSnsClient(
            logger,
            new BasicAWSCredentials(localStackOptions.AccessKeyId, localStackOptions.SecretAccessKey),
            new AmazonSimpleNotificationServiceConfig
            {
                // https://github.com/aws/aws-sdk-net/issues/1781
                AuthenticationRegion = localStackOptions.AwsRegion ?? RegionEndpoint.EUWest2.ToString(),
                RegionEndpoint = RegionEndpoint.GetBySystemName(
                    localStackOptions.AwsRegion ?? RegionEndpoint.EUWest2.ToString()
                ),
                ServiceURL = localStackOptions.SnsEndpoint,
            }
        );
    });

    services.AddSingleton<IAmazonSQS>(sp =>
    {
        var localStackOptions = sp.GetRequiredService<IOptions<LocalStackOptions>>().Value;
        if (localStackOptions.UseLocalStack == false)
            return new AmazonSQSClient();

        return new AmazonSQSClient(
            new BasicAWSCredentials(localStackOptions.AccessKeyId, localStackOptions.SecretAccessKey),
            new AmazonSQSConfig
            {
                // https://github.com/aws/aws-sdk-net/issues/1781
                AuthenticationRegion = localStackOptions.AwsRegion ?? RegionEndpoint.EUWest2.ToString(),
                RegionEndpoint = RegionEndpoint.GetBySystemName(
                    localStackOptions.AwsRegion ?? RegionEndpoint.EUWest2.ToString()
                ),
                ServiceURL = localStackOptions.SqsEndpoint,
            }
        );
    });

    services.AddScheduler(configuration);
    services.AddDbContext(configuration, integrationTest);
    services.AddSingleton<ICronJob, ExampleJob>();
    services.AddScoped<IJobMiddleware, JobLeaseJobMiddleware>();
}

[ExcludeFromCodeCoverage]
static void ConfigureMiddleware(WebApplication app)
{
    app.UseSerilogRequestLogging();

    app.UseHeaderPropagation();
}

[ExcludeFromCodeCoverage]
static void ConfigureEndpoints(WebApplication app)
{
    app.MapHealth();

    // Remove before deploying
    ////app.MapExampleEndpoints();
}

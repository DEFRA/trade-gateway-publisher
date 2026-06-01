using System.Diagnostics.CodeAnalysis;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Defra.TradeImports.EmfExporter;
using Defra.TradeImports.Tracing;
using Infrastructure;
using Infrastructure.Data.Extensions;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Extensions;
using Infrastructure.Resilience;
using Infrastructure.Scheduler;
using Infrastructure.Scheduler.Extensions;
using Infrastructure.TracesGateway.Extensions;
using Microsoft.Extensions.Options;
using Serilog;
using TradeGatewayPublisher.Config;
using TradeGatewayPublisher.Features.IntraChanges;
using TradeGatewayPublisher.Health;
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
    app.UseEmfExporter(MetricNames.MeterName);

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
    services.AddTracesGateway(configuration);
    services.AddSingleton<IMessageConsumer, IntraUpdateConsumer>();
    services.AddMessaging(
        configuration,
        sp => sp.GetRequiredService<IOptions<TracesUpdateConsumerOptions>>().Value.IntraQueueUrl
    );
    services.AddHttpContextAccessor();
    services.AddTraceContextAccessor(configuration);

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
    services
        .AddOptions<TracesUpdatePublisherOptions>()
        .Bind(configuration.GetSection(TracesUpdatePublisherOptions.SectionName))
        .ValidateOnStart();
    services
        .AddOptions<TracesUpdateConsumerOptions>()
        .Bind(configuration.GetSection(TracesUpdateConsumerOptions.SectionName))
        .ValidateOnStart();

    services.AddScoped<IJobMiddleware, JobMetricsMiddleware>();
    services.AddScoped<IJobMiddleware, JobLeaseMiddleware>();
    services.AddScoped<IJobMiddleware, JobWatermarkMiddleware>();
    services.AddScheduler(configuration);
    services.AddDbContext(configuration, integrationTest);
    services.AddSingleton<ICronJob, TracesIntraChangesJob>();
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

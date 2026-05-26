using System.Diagnostics.CodeAnalysis;
using Infrastructure.Data.Extensions;
using Infrastructure.Scheduler;
using Infrastructure.Scheduler.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MongoDB.Driver;
using Serilog;
using TradeGatewayPublisher.Jobs;
using TradeGatewayPublisher.Jobs.Middleware;
using TradeGatewayPublisher.Utils;
using TradeGatewayPublisher.Utils.Http;
using TradeGatewayPublisher.Utils.Logging;
using MongoConfig = TradeGatewayPublisher.Config.MongoConfig;

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
    const string Extended = "extended";

    var services = builder.Services;
    var configuration = builder.Configuration;

    // Trust material must be loaded before anything creates outbound connections.
    services.LoadCustomTrustStoreFromEnvironment();

    services.AddProblemDetails();
    services.AddValidation();

    services.AddHttpContextAccessor();

    ConfigureHeaderPropagation(services, configuration);
    ConfigureHttpClients(services);
    ConfigureMongo(services, configuration, integrationTest);

    services
        .AddHealthChecks()
        .AddMongoDb(
            provider => provider.GetRequiredService<IMongoDatabase>(),
            timeout: TimeSpan.FromSeconds(10),
            tags: [Extended]
        );
    ////.AddSns(
    ////    "Upserts topic",
    ////    sp => sp.GetRequiredService<IOptions<ResourceEventOptions>>().Value.TopicArn,
    ////    tags: [Extended],
    ////    timeout: TimeSpan.FromSeconds(10)
    ////)
    ////.AddSqs(
    ////    configuration,
    ////    "Data events SQS queue",
    ////    _ =>
    ////        configuration.GetValue<string>("DATA_EVENTS_QUEUE_NAME")
    ////        ?? throw new InvalidOperationException("Missing DATA_EVENTS_QUEUE_NAME"),
    ////    timeout: TimeSpan.FromSeconds(10),
    ////    tags: [Extended]
    ////);

    // App services
    ////services.AddSingleton<IExamplePersistence, ExamplePersistence>();
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

    //// services.AddHttpClientWithTracing<IExampleClient, ExampleClient>();
    //// services.AddHttpClientWithProxy<IExternalClient, ExternalClient>();
}

[ExcludeFromCodeCoverage]
static void ConfigureMongo(IServiceCollection services, IConfiguration configuration, bool integrationTest)
{
    services.AddScheduler(configuration);
    services.AddDbContext(configuration, integrationTest);
    services.AddSingleton<ICronJob, ExampleJob>();
    services.AddSingleton<IJobMiddleware, JobLeaseJobMiddleware>();
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
    app.MapHealthChecks("/health", new HealthCheckOptions());

    // Remove before deploying
    ////app.MapExampleEndpoints();
}

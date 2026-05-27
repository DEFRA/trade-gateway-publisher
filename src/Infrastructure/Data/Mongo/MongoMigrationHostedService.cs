using System.Reflection;
using AdaskoTheBeAsT.MongoDbMigrations;
using Infrastructure.Leasing;
using Infrastructure.Scheduler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Infrastructure.Data.Mongo;

public class MongoMigrationHostedService(
    ILogger<MongoMigrationHostedService> logger,
    IMongoDatabase mongoDatabase,
    IServiceScopeFactory scopeFactory
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Mongo Migrations starting.");
        var leaseName = $"mongo-migrations";

        using var scope = scopeFactory.CreateScope();

        var leaseProvider = scope.ServiceProvider.GetRequiredService<ILeaseProvider>();

        await using var lease = await leaseProvider.TryAcquireAsync(leaseName, TimeSpan.FromMinutes(10), stoppingToken);

        if (lease is null)
        {
            logger.LogInformation("Skipping {JobName} because lease could not be acquired", leaseName);

            return;
        }

        using var engine = new MigrationEngineBuilder().UseDatabase(
            mongoDatabase.Client,
            mongoDatabase.DatabaseNamespace.DatabaseName
        );

        var result = await engine
            .UseAssembly(Assembly.GetExecutingAssembly())
            .UseSchemeValidation(false)
            .RunAsync(stoppingToken);

        if (!result.Success)
            throw new InvalidOperationException("Mongo Migrations Failed");

        logger.LogInformation("Mongo Migrations completed.");
    }
}

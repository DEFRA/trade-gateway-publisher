using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Data.Entities;
using Data.Mongo.Migrations;
using MongoDB.Driver;
using Version = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.TradeImportsDataApi.Data.Mongo.Migrations;

public class AddJobLeaseEntityIndexes()
    : Migration("Add indexes to Job_Lease collection", new Version(1, 0, 1))
{
    public override async Task UpAsync(MigrationContext context)
    {
        var collection = context.Database.GetCollection<JobLeaseEntity>(typeof(JobLeaseEntity).DataEntityName());

        await CreateTtlIndex(
            collection,
            "ExpiresAtTtlIdx",
            Builders<JobLeaseEntity>.IndexKeys.Ascending(x => x.ExpiresAtUtc),
            cancellationToken: context.CancellationToken
        );
    }

    public override async Task DownAsync(MigrationContext context)
    {
        var collection = context.Database.GetCollection<JobLeaseEntity>(typeof(JobLeaseEntity).DataEntityName());

        await collection.Indexes.DropOneAsync("ExpiresAtTtlIdx", context.CancellationToken);
    }
}

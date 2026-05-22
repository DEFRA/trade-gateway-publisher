using Data;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace CronJobs.Leasing;

public sealed class JobLeaseProvider(IMongoDbClientFactory mongoDbClientFactory, ILogger<JobLeaseProvider> logger)
    : IJobLeaseProvider
{
    private const string CollectionName = "job_leases";

    private readonly IMongoCollection<JobLeaseDocument> _collection =
        mongoDbClientFactory.GetCollection<JobLeaseDocument>(CollectionName);

    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public async Task<IAsyncDisposable?> TryAcquireAsync(
        string leaseName,
        TimeSpan duration,
        CancellationToken cancellationToken
    )
    {
        var now = DateTime.UtcNow;

        var expiresAt = now.Add(duration);

        var filter = Builders<JobLeaseDocument>.Filter.And(
            Builders<JobLeaseDocument>.Filter.Eq(x => x.Name, leaseName),
            Builders<JobLeaseDocument>.Filter.Lte(x => x.ExpiresAtUtc, now)
        );

        var replacement = new JobLeaseDocument
        {
            Name = leaseName,
            Owner = _instanceId,
            ExpiresAtUtc = expiresAt,
        };

        try
        {
            // Try replacing expired lease
            var result = await _collection.ReplaceOneAsync(
                filter,
                replacement,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken
            );

            if (result.ModifiedCount > 0 || result.UpsertedId != null)
            {
                logger.LogInformation("Acquired lease {LeaseName} until {ExpiresAt}", leaseName, expiresAt);

                return new JobLeaseHandle(_collection, leaseName, _instanceId);
            }

            logger.LogInformation("Lease {LeaseName} already held by another instance", leaseName);

            return null;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            ////logger.LogInformation("Lease contention detected for {LeaseName}", leaseName);

            return null;
        }
    }

    public static async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var collection = database.GetCollection<JobLeaseDocument>(CollectionName);

        // unique lease name
        var uniqueIndex = new CreateIndexModel<JobLeaseDocument>(
            Builders<JobLeaseDocument>.IndexKeys.Ascending(x => x.Name),
            new CreateIndexOptions { Unique = true }
        );

        // automatic cleanup
        var ttlIndex = new CreateIndexModel<JobLeaseDocument>(
            Builders<JobLeaseDocument>.IndexKeys.Ascending(x => x.ExpiresAtUtc),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }
        );

        await collection.Indexes.CreateManyAsync([uniqueIndex, ttlIndex], cancellationToken);
    }
}

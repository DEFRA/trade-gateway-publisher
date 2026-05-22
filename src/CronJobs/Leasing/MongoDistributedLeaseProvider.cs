using Data;
using Data.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace CronJobs.Leasing;

public sealed class JobLeaseProvider(IDbContext db, ILogger<JobLeaseProvider> logger)
    : IJobLeaseProvider
{
    private readonly IMongoCollectionSet<JobLeaseEntity> _collection = db.Set<JobLeaseEntity>();

    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public async Task<IAsyncDisposable?> TryAcquireAsync(
        string leaseName,
        TimeSpan duration,
        CancellationToken cancellationToken
    )
    {
        var now = DateTime.UtcNow;

        var expiresAt = now.Add(duration);

        var filter = Builders<JobLeaseEntity>.Filter.And(
            Builders<JobLeaseEntity>.Filter.Eq(x => x.Id, leaseName),
            Builders<JobLeaseEntity>.Filter.Lte(x => x.ExpiresAtUtc, now)
        );

        var replacement = new JobLeaseEntity
        {
            Id = leaseName,
            Owner = _instanceId,
            ExpiresAtUtc = expiresAt,
            ETag = Guid.CreateVersion7().ToString(),
        };

        try
        {
            // Try replacing expired lease
            var result = await _collection.Collection.ReplaceOneAsync(
                filter,
                replacement,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken
            );

            if (result.ModifiedCount > 0 || result.UpsertedId != null)
            {
                logger.LogInformation("Acquired lease {LeaseName} until {ExpiresAt}", leaseName, expiresAt);

                return new JobLeaseHandle(_collection.Collection, leaseName, _instanceId);
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
}

using Data;
using Data.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace CronJobs.Watermark;

public sealed class MongoJobWatermarkStore(IDbContext database, ILogger<MongoJobWatermarkStore> logger)
    : IJobWatermarkStore
{
    private readonly IMongoCollection<JobWatermarkEntity> _collection = database.Set<JobWatermarkEntity>().Collection;

    public async Task<DateTimeOffset?> GetAsync(string jobName, CancellationToken cancellationToken = default)
    {
        var filter = Builders<JobWatermarkEntity>.Filter.Eq(x => x.Id, jobName);

        var document = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            logger.LogInformation("No watermark found for job {JobName}", jobName);

            return null;
        }

        logger.LogDebug("Loaded watermark {Watermark} for job {JobName}", document.WatermarkUtc, jobName);

        return new DateTimeOffset(DateTime.SpecifyKind(document.WatermarkUtc, DateTimeKind.Utc));
    }

    public async Task SetAsync(string jobName, DateTimeOffset watermark, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var filter = Builders<JobWatermarkEntity>.Filter.Eq(x => x.Id, jobName);

        var update = Builders<JobWatermarkEntity>
            .Update.Set(x => x.WatermarkUtc, watermark.UtcDateTime)
            .Set(x => x.UpdatedAtUtc, now);

        await _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);

        logger.LogInformation("Updated watermark for {JobName} to {Watermark}", jobName, watermark);
    }
}

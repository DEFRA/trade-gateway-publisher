using Infrastructure.Data;
using Infrastructure.Data.Entities;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Watermark;

public sealed class JobWatermarkStore(IDbContext database, ILogger<JobWatermarkStore> logger) : IJobWatermarkStore
{
    private readonly IMongoCollectionSet<JobWatermarkEntity> _collection = database.Watermarks;

    public async Task<DateTimeOffset?> GetAsync(string jobName, CancellationToken cancellationToken = default)
    {
        var document = await _collection.Find(jobName, cancellationToken);

        if (document is null)
        {
            logger.LogInformation("No watermark found for job {JobName}", jobName);

            return null;
        }

        logger.LogDebug("Loaded watermark {Watermark} for job {JobName}", document.Watermark, jobName);

        return new DateTimeOffset(DateTime.SpecifyKind(document.Watermark, DateTimeKind.Utc));
    }

    public async Task SetAsync(string jobName, DateTimeOffset watermark, CancellationToken cancellationToken = default)
    {
        var entity = new JobWatermarkEntity() { Id = jobName, Watermark = watermark.UtcDateTime };
        _collection.Upsert(entity);
        await _collection.Save(cancellationToken);
        logger.LogInformation("Updated watermark for {JobName} to {Watermark}", jobName, watermark);
    }
}

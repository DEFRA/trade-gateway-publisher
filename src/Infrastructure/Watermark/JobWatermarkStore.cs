using Infrastructure.Data.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Infrastructure.Watermark;

public sealed class JobWatermarkStore(
    IMongoCollection<JobWatermarkEntity> collection,
    ILogger<JobWatermarkStore> logger
) : IJobWatermarkStore
{
    public async Task<DateTimeOffset?> GetAsync(string jobName, CancellationToken cancellationToken = default)
    {
        var document = await (
            await collection.FindAsync(x => x.Id == jobName, cancellationToken: cancellationToken)
        ).FirstOrDefaultAsync(cancellationToken: cancellationToken);

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
        var entity = new JobWatermarkEntity()
        {
            Id = jobName,
            Watermark = watermark.UtcDateTime,
            UpdatedAt = DateTime.UtcNow,
        };
        await collection.ReplaceOneAsync(
            x => x.Id == jobName,
            entity,
            new ReplaceOptions() { IsUpsert = true },
            cancellationToken
        );
        logger.LogInformation("Updated watermark for {JobName} to {Watermark}", jobName, watermark);
    }
}

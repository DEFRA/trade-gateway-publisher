using Infrastructure.Data.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Infrastructure.Leasing;

public sealed class LeaseProvider(IMongoCollection<LeaseEntity> collection, ILogger<LeaseProvider> logger)
    : ILeaseProvider
{
    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public async Task<IAsyncDisposable?> TryAcquireAsync(
        string leaseName,
        TimeSpan duration,
        CancellationToken cancellationToken
    )
    {
        var now = DateTime.UtcNow;

        var expiresAt = now.Add(duration);

        var replacement = new LeaseEntity
        {
            Id = leaseName,
            Owner = _instanceId,
            ExpiresAt = expiresAt,
            CreatedAt = now,
        };

        try
        {
            await collection.InsertOneAsync(replacement, cancellationToken: cancellationToken);
            logger.LogInformation("Acquired lease {LeaseName} until {ExpiresAt}", leaseName, expiresAt);
            return new LeaseHandle(collection, leaseName, _instanceId);
        }
        catch (MongoWriteException ex)
        {
            logger.LogDebug(ex, "Lease already exists for {LeaseName}", leaseName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Lease contention detected for {LeaseName}", leaseName);
            return null;
        }
    }
}

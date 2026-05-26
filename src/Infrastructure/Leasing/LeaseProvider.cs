using Infrastructure.Data;
using Infrastructure.Data.Entities;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Leasing;

public sealed class LeaseProvider(IDbContext db, ILogger<LeaseProvider> logger) : ILeaseProvider
{
    private readonly IMongoCollectionSet<LeaseEntity> _collection = db.Leases;

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
            ExpiresAtUtc = expiresAt,
        };

        try
        {
            _collection.Insert(replacement);
            await _collection.Save(cancellationToken);
            logger.LogInformation("Acquired lease {LeaseName} until {ExpiresAt}", leaseName, expiresAt);
            return new LeaseHandle(_collection, leaseName, _instanceId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Lease contention detected for {LeaseName}", leaseName);
            return null;
        }
    }
}

using Infrastructure.Data.Entities;

namespace Infrastructure.Data;

public interface IDbContext
{
    IMongoCollectionSet<JobWatermarkEntity> Watermarks { get; }

    IMongoCollectionSet<LeaseEntity> Leases { get; }

    Task SaveChanges(CancellationToken cancellationToken);
}

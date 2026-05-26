using Infrastructure.Data;
using Infrastructure.Data.Entities;

namespace Testing.Data.InMemoryData;

public class MemoryDbContext : IDbContext
{
    public IMongoCollectionSet<LeaseEntity> Leases { get; } = new MemoryCollectionSet<LeaseEntity>();

    public IMongoCollectionSet<JobWatermarkEntity> Watermarks { get; } = new MemoryCollectionSet<JobWatermarkEntity>();

    public Task SaveChanges(CancellationToken cancellationToken) => throw new NotImplementedException();
}

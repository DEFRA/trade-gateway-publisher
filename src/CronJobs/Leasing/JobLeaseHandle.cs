using Data.Entities;
using MongoDB.Driver;

namespace CronJobs.Leasing;

internal sealed class JobLeaseHandle(IMongoCollection<LeaseEntity> collection, string leaseName, string owner)
    : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        var filter = Builders<LeaseEntity>.Filter.And(
            Builders<LeaseEntity>.Filter.Eq(x => x.Id, leaseName),
            Builders<LeaseEntity>.Filter.Eq(x => x.Owner, owner)
        );

        await collection.DeleteOneAsync(filter);
    }
}

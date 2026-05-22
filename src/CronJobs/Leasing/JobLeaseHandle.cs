using Data.Entities;
using MongoDB.Driver;

namespace CronJobs.Leasing;

internal sealed class JobLeaseHandle(IMongoCollection<JobLeaseEntity> collection, string leaseName, string owner)
    : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        var filter = Builders<JobLeaseEntity>.Filter.And(
            Builders<JobLeaseEntity>.Filter.Eq(x => x.Id, leaseName),
            Builders<JobLeaseEntity>.Filter.Eq(x => x.Owner, owner)
        );

        await collection.DeleteOneAsync(filter);
    }
}

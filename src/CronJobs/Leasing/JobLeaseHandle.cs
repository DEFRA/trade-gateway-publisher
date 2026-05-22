using MongoDB.Driver;

namespace CronJobs.Leasing;

internal sealed class JobLeaseHandle(IMongoCollection<JobLeaseDocument> collection, string leaseName, string owner)
    : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        var filter = Builders<JobLeaseDocument>.Filter.And(
            Builders<JobLeaseDocument>.Filter.Eq(x => x.Name, leaseName),
            Builders<JobLeaseDocument>.Filter.Eq(x => x.Owner, owner)
        );

        await collection.DeleteOneAsync(filter);
    }
}

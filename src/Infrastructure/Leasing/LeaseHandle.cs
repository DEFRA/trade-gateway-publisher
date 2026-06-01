using Infrastructure.Data.Entities;
using MongoDB.Driver;

namespace Infrastructure.Leasing;

internal sealed class LeaseHandle(IMongoCollection<LeaseEntity> collection, string leaseName, string owner)
    : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await collection.DeleteOneAsync(x => x.Id == leaseName && x.Owner == owner, CancellationToken.None);
    }
}

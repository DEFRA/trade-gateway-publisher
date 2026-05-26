using Infrastructure.Data;
using Infrastructure.Data.Entities;
using MongoDB.Driver;

namespace Infrastructure.Leasing;

internal sealed class LeaseHandle(IMongoCollectionSet<LeaseEntity> collection, string leaseName, string owner)
    : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        collection.Delete(new LeaseEntity() { Id = leaseName, Owner = owner });
        await collection.Save(CancellationToken.None);
    }
}

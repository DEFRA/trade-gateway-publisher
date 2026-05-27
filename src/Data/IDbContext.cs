using Data.Entities;

namespace Data;

public interface IDbContext
{
    IMongoCollectionSet<T> Set<T>()
        where T : class, IDataEntity;

    Task SaveChanges(CancellationToken cancellationToken);
}

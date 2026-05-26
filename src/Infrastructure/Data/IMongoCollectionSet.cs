using Infrastructure.Data.Entities;
using MongoDB.Driver;

namespace Infrastructure.Data;

public interface IMongoCollectionSet<T> : IQueryable<T>
    where T : IDataEntity
{
    IMongoCollection<T> Collection { get; }

    Task<T?> Find(string id, CancellationToken cancellationToken);

    Task Save(CancellationToken cancellationToken);

    void Upsert(T item);

    void Insert(T item);

    void Delete(T item);
}

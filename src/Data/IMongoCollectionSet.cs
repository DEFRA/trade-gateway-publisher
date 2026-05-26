using System.Linq.Expressions;
using Data.Entities;
using MongoDB.Driver;

namespace Data;

public interface IMongoCollectionSet<T> : IQueryable<T>
    where T : IDataEntity
{
    IMongoCollection<T> Collection { get; }

    Task<T?> Find(string id, CancellationToken cancellationToken);

    Task<List<T>> FindMany(Expression<Func<T, bool>> query, CancellationToken cancellationToken);

    void Insert(T item);

    void Update(T item, string etag);

    Task Save(CancellationToken cancellationToken);

    void Upsert(T item, string etag);
}

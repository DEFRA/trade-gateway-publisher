using System.Collections;
using System.Linq.Expressions;
using Infrastructure.Data;
using Infrastructure.Data.Entities;
using MongoDB.Driver;

namespace Testing.Data.InMemoryData;

public class MemoryCollectionSet<T> : IMongoCollectionSet<T>
    where T : IDataEntity
{
    private readonly List<T> _data = [];

    private IQueryable<T> EntityQueryable => _data.AsQueryable();

    public IEnumerator<T> GetEnumerator() => _data.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public Type ElementType => EntityQueryable.ElementType;
    public Expression Expression => EntityQueryable.Expression;
    public IQueryProvider Provider => EntityQueryable.Provider;

    public IMongoCollection<T> Collection => throw new NotImplementedException();

    internal void AddTestData(T item) => _data.Add(item);

    public Task<T?> Find(string id, CancellationToken cancellationToken) =>
        Task.FromResult(_data.Find(x => x.Id == id));

    public Task Save(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Upsert(T item)
    {
        _data.RemoveAll(x => x.Id == item.Id);
        if (item.Created == default)
            item.Created = item.Updated = DateTime.UtcNow;
        else
            item.Updated = DateTime.UtcNow;

        _data.Add(item);
    }

    public void Insert(T item)
    {
        if (_data.Exists(x => x.Id == item.Id))
            throw new InvalidOperationException("Item with the same ID already exists.");

        item.Created = item.Updated = DateTime.UtcNow;
        _data.Add(item);
    }

    public void Delete(T item)
    {
        _data.RemoveAll(x => x.Id == item.Id);
    }
}

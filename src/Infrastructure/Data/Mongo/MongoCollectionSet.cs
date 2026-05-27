using System.Collections;
using System.Linq.Expressions;
using Infrastructure.Data.Entities;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Infrastructure.Data.Mongo;

public class MongoCollectionSet<T>(MongoDbContext dbContext, string collectionName = null!) : IMongoCollectionSet<T>
    where T : class, IDataEntity
{
    private readonly List<T> _entitiesToUpsert = [];
    private readonly List<T> _entitiesToInsert = [];
    private readonly List<T> _entitiesToDelete = [];

    private IQueryable<T> EntityQueryable => Collection.AsQueryable();

    public IEnumerator<T> GetEnumerator() => EntityQueryable.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => EntityQueryable.GetEnumerator();

    public Type ElementType => EntityQueryable.ElementType;
    public Expression Expression => EntityQueryable.Expression;
    public IQueryProvider Provider => EntityQueryable.Provider;

    public IMongoCollection<T> Collection { get; } =
        string.IsNullOrEmpty(collectionName)
            ? dbContext.Database.GetCollection<T>(typeof(T).DataEntityName())
            : dbContext.Database.GetCollection<T>(collectionName);

    public async Task<T?> Find(string id, CancellationToken cancellationToken) =>
        await EntityQueryable.SingleOrDefaultAsync(x => x.Id == id, cancellationToken: cancellationToken);

    public async Task Save(CancellationToken cancellationToken)
    {
        await Insert(cancellationToken);
        await Upsert(cancellationToken);
        await Delete(cancellationToken);
    }

    public void Upsert(T item)
    {
        if (_entitiesToUpsert.Exists(x => x.Id == item.Id))
            return;

        if (item.Created == default)
            item.Created = item.Updated = DateTime.UtcNow;
        else
            item.Updated = DateTime.UtcNow;

        _entitiesToUpsert.Add(item);
    }

    public void Insert(T item)
    {
        if (_entitiesToInsert.Exists(x => x.Id == item.Id))
            return;

        if (item.Created == default)
            item.Created = item.Updated = DateTime.UtcNow;
        else
            item.Updated = DateTime.UtcNow;

        _entitiesToInsert.Add(item);
    }

    public void Delete(T item)
    {
        if (_entitiesToDelete.Exists(x => x.Id == item.Id))
            return;

        _entitiesToDelete.Add(item);
    }

    private async Task Delete(CancellationToken cancellationToken)
    {
        var builder = Builders<T>.Filter;

        if (_entitiesToDelete.Count != 0)
        {
            foreach (var item in _entitiesToDelete)
            {
                var filter = builder.Eq(x => x.Id, item.Id);

                await Collection.DeleteOneAsync(filter, cancellationToken: cancellationToken);
            }

            _entitiesToDelete.Clear();
        }
    }

    private async Task Upsert(CancellationToken cancellationToken)
    {
        var builder = Builders<T>.Filter;

        if (_entitiesToUpsert.Count != 0)
        {
            foreach (var item in _entitiesToUpsert)
            {
                var filter = builder.Eq(x => x.Id, item.Id);

                var updateResult = await Collection.ReplaceOneAsync(
                    filter,
                    item,
                    new ReplaceOptions() { IsUpsert = true },
                    cancellationToken: cancellationToken
                );

                if (updateResult.ModifiedCount == 0)
                    throw new ConcurrencyException(item.Id, string.Empty);
            }

            _entitiesToUpsert.Clear();
        }
    }

    private async Task Insert(CancellationToken cancellationToken)
    {
        if (_entitiesToInsert.Count != 0)
        {
            foreach (var item in _entitiesToInsert)
            {
                await Collection.InsertOneAsync(item, cancellationToken: cancellationToken);
            }

            _entitiesToInsert.Clear();
        }
    }
}

using System.Collections;
using System.Linq.Expressions;
using Data.Entities;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Data.Mongo;

public class MongoCollectionSet<T>(MongoDbContext dbContext, string collectionName = null!) : IMongoCollectionSet<T>
    where T : class, IDataEntity
{
    private readonly List<T> _entitiesToInsert = [];
    private readonly List<(T Item, string Etag)> _entitiesToUpdate = [];
    private readonly List<(string Id, UpdateDefinition<T> Patch, string Etag)> _entitiesToPatch = [];

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

    public async Task<List<T>> FindMany(Expression<Func<T, bool>> query, CancellationToken cancellationToken) =>
        await EntityQueryable.Where(query).ToListAsync(cancellationToken);

    public async Task Save(CancellationToken cancellationToken)
    {
        await Insert(cancellationToken);
        await Update(cancellationToken);
    }

    public void Upsert(T item, string etag)
    {
        throw new NotImplementedException();
    }

    private async Task Update(CancellationToken cancellationToken)
    {
        var builder = Builders<T>.Filter;

        if (_entitiesToUpdate.Count != 0)
        {
            foreach (var item in _entitiesToUpdate)
            {
                var filter = builder.Eq(x => x.Id, item.Item.Id) & builder.Eq(x => x.ETag, item.Etag);

                var updateResult = await Collection.ReplaceOneAsync(
                    filter,
                    item.Item,
                    cancellationToken: cancellationToken
                );

                if (updateResult.ModifiedCount == 0)
                    throw new ConcurrencyException(item.Item.Id, item.Etag);
            }

            _entitiesToUpdate.Clear();
        }

        if (_entitiesToPatch.Count != 0)
        {
            foreach (var item in _entitiesToPatch)
            {
                var filter = builder.Eq(x => x.Id, item.Id) & builder.Eq(x => x.ETag, item.Etag);

                var updateResult = await Collection.UpdateOneAsync(
                    filter,
                    item.Patch,
                    cancellationToken: cancellationToken
                );

                if (updateResult.ModifiedCount == 0)
                    throw new ConcurrencyException(item.Id, item.Etag);
            }

            _entitiesToPatch.Clear();
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

    public void Insert(T item)
    {
        // Update in memory item now but will only be saved if Save is called
        item.Created = item.Updated = DateTime.UtcNow;
        item.ETag = BsonObjectIdGenerator.Instance.GenerateId(null, null).ToString()!;
        item.OnSave();

        _entitiesToInsert.Add(item);
    }

    public void Update(T item, string etag)
    {
        if (_entitiesToInsert.Exists(x => x.Id == item.Id))
            return;

        ArgumentNullException.ThrowIfNull(etag);

        _entitiesToUpdate.RemoveAll(x => x.Item.Id == item.Id);

        // Update in memory item now but will only be saved if Save is called
        item.Updated = DateTime.UtcNow;
        item.ETag = BsonObjectIdGenerator.Instance.GenerateId(null, null).ToString()!;
        item.OnSave();

        _entitiesToUpdate.Add(new ValueTuple<T, string>(item, etag));
    }
}

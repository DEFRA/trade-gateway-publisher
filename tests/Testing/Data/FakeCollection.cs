using System.Data;
using Infrastructure.Data.Entities;
using MongoDB.Driver;
using NSubstitute;

namespace Testing.Data;

public sealed class FakeCollection<T>
    where T : IDataEntity
{
    public readonly List<T> _items = [];

    public IMongoCollection<T> Collection { get; }

    public FakeCollection()
    {
        Collection = Substitute.For<IMongoCollection<T>>();

        ConfigureFind();
        ConfigureReplace();
        ConfigureInsert();
        ConfigureDelete();
    }

    public void Add(T item) => _items.Add(item);

    public void AddRange(IEnumerable<T> items) => _items.AddRange(items);

    private void ConfigureFind()
    {
        Collection
            .FindAsync(Arg.Any<FilterDefinition<T>>(), Arg.Any<FindOptions<T, T>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                T? match = default;
                if (call.Arg<FilterDefinition<T>>() is ExpressionFilterDefinition<T> filterDefinition)
                {
                    var predicate = filterDefinition.Expression.Compile();
                    match = _items.FirstOrDefault(predicate);
                }

                var cursor = Substitute.For<IAsyncCursor<T>>();

                var batch = match is null ? Array.Empty<T>() : new[] { match };

                cursor.Current.Returns(batch);

                var first = true;

                cursor
                    .MoveNext(Arg.Any<CancellationToken>())
                    .Returns(_ =>
                    {
                        if (!first)
                            return false;

                        first = false;
                        return batch.Length > 0;
                    });

                cursor
                    .MoveNextAsync(Arg.Any<CancellationToken>())
                    .Returns(_ =>
                    {
                        if (!first)
                            return Task.FromResult(false);

                        first = false;
                        return Task.FromResult(batch.Length > 0);
                    });

                return Task.FromResult(cursor);
            });
    }

    private void ConfigureReplace()
    {
        Collection
            .ReplaceOneAsync(
                Arg.Any<FilterDefinition<T>>(),
                Arg.Any<T>(),
                Arg.Any<ReplaceOptions>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(call =>
            {
                if (call.Arg<FilterDefinition<T>>() is ExpressionFilterDefinition<T> filterDefinition)
                {
                    var predicate = filterDefinition.Expression.Compile();

                    var replacement = call.Arg<T>();

                    var index = _items.FindIndex(x => predicate(x));

                    if (index >= 0)
                    {
                        _items[index] = replacement;
                    }
                    else
                    {
                        _items.Add(replacement);
                    }

                    var result = Substitute.For<ReplaceOneResult>();

                    result.IsAcknowledged.Returns(true);
                    result.MatchedCount.Returns(index >= 0 ? 1 : 0);
                    result.ModifiedCount.Returns(1);

                    return result;
                }

                return Substitute.For<ReplaceOneResult>();
            });
    }

    private void ConfigureInsert()
    {
        Collection
            .InsertOneAsync(Arg.Any<T>(), Arg.Any<InsertOneOptions>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var replacement = call.Arg<T>();
                if (_items.Exists(x => x.Id == replacement.Id))
                {
                    throw new DuplicateNameException("Duplicate key error", null);
                }
                _items.Add(replacement);
                return Task.CompletedTask;
            });
    }

    private void ConfigureDelete()
    {
        Collection
            .DeleteOneAsync(Arg.Any<FilterDefinition<T>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (call.Arg<FilterDefinition<T>>() is ExpressionFilterDefinition<T> filterDefinition)
                {
                    var predicate = filterDefinition.Expression.Compile();

                    _items.RemoveAll(x => predicate(x));

                    var result = Substitute.For<DeleteResult>();

                    result.IsAcknowledged.Returns(true);
                    result.DeletedCount.Returns(1);

                    return result;
                }

                return Substitute.For<DeleteResult>();
            });
    }
}

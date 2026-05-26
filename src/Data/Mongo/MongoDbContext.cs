using System.Diagnostics.CodeAnalysis;
using Data.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Data.Mongo;

[ExcludeFromCodeCoverage]
public class MongoDbContext : IDbContext
{
    private readonly ILogger<MongoDbContext> _logger;

    public MongoDbContext(IMongoDatabase database, ILogger<MongoDbContext> logger)
    {
        _logger = logger;

        Database = database;
    }

    internal IMongoDatabase Database { get; }

    public IMongoCollectionSet<T> Set<T>()
        where T : class, IDataEntity
    {
        return new MongoCollectionSet<T>(this);
    }

    public async Task SaveChanges(CancellationToken cancellationToken)
    {
        try
        {
            ////await ImportPreNotifications.Save(cancellationToken);
            ////await CustomsDeclarations.Save(cancellationToken);
            ////await Gmrs.Save(cancellationToken);
            ////await ProcessingErrors.Save(cancellationToken);
            ////await ResourceEvents.Save(cancellationToken);

            ////// Keep this last as upserts above will impact those below
            ////await ImportPreNotificationUpdates.Save(cancellationToken);
        }
        catch (MongoCommandException mongoCommandException) when (mongoCommandException.Code == 112)
        {
            const string message = "Mongo write conflict - consumer will retry";
            _logger.LogWarning(mongoCommandException, message);

            // WriteConflict error: this operation conflicted with another operation. Please retry your operation or multi-document transaction
            // - retries are built into consumers of the data API
            throw new ConcurrencyException(message, mongoCommandException);
        }
        catch (MongoWriteException mongoWriteException) when (mongoWriteException.WriteError.Code == 11000)
        {
            const string message = "Mongo write error - consumer will retry";
            _logger.LogWarning(mongoWriteException, message);

            // A write operation resulted in an error. WriteError: { Category : "DuplicateKey", Code : 11000 }
            // - retries are built into consumers of the data API
            throw new ConcurrencyException(message, mongoWriteException);
        }
    }
}

using Data.Entities;

namespace Data;

public interface IDbContext
{
    ////IMongoCollectionSet<ImportPreNotificationEntity> ImportPreNotifications { get; }

    ////IMongoCollectionSet<ImportPreNotificationUpdateEntity> ImportPreNotificationUpdates { get; }

    ////IMongoCollectionSet<CustomsDeclarationEntity> CustomsDeclarations { get; }

    ////IMongoCollectionSet<GmrEntity> Gmrs { get; }

    ////IMongoCollectionSet<ProcessingErrorEntity> ProcessingErrors { get; }
    ////IMongoCollectionSet<ResourceEventEntity> ResourceEvents { get; }

    IMongoCollectionSet<T> Set<T>() where T : class, IDataEntity;

    Task SaveChanges(CancellationToken cancellationToken);

    Task StartTransaction(CancellationToken cancellationToken);

    Task CommitTransaction(CancellationToken cancellationToken);
}

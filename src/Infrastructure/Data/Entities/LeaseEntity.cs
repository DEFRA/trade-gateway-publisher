using MongoDB.Bson.Serialization.Attributes;

namespace Infrastructure.Data.Entities;

public sealed class LeaseEntity : IDataEntity
{
    [BsonId]
    public required string Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public required string Owner { get; set; }
    public DateTime ExpiresAt { get; set; }
}

using MongoDB.Bson.Serialization.Attributes;

namespace Infrastructure.Data.Entities;

public sealed class JobWatermarkEntity : IDataEntity
{
    [BsonId]
    public required string Id { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime Watermark { get; set; }
}

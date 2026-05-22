using MongoDB.Bson.Serialization.Attributes;

namespace CronJobs.Watermark
{
    public sealed class JobWatermarkDocument
    {
        [BsonId]
        public required string Id { get; set; }

        [BsonElement("watermarkUtc")]
        public DateTime WatermarkUtc { get; set; }

        [BsonElement("updatedAtUtc")]
        public DateTime UpdatedAtUtc { get; set; }
    }
}

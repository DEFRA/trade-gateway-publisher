using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CronJobs.Leasing
{
    public sealed class JobLeaseDocument
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("name")]
        public required string Name { get; set; }

        [BsonElement("owner")]
        public required string Owner { get; set; }

        [BsonElement("expiresAtUtc")]
        public DateTime ExpiresAtUtc { get; set; }
    }
}

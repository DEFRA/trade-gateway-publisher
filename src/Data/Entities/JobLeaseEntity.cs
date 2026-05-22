using Data.Configuration;
using MongoDB.Bson.Serialization.Attributes;

namespace Data.Entities
{
    [DbCollection("job_leases")]
    public sealed class JobLeaseEntity : IDataEntity
    {
        [BsonId]
        public required string Id { get; set; }

        public required string ETag { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public void OnSave()
        {
            
        }

        [BsonElement("owner")]
        public required string Owner { get; set; }

        [BsonElement("expiresAtUtc")]
        public DateTime ExpiresAtUtc { get; set; }
    }
}

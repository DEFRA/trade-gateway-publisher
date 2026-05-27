using Infrastructure.Data.Configuration;
using MongoDB.Bson.Serialization.Attributes;

namespace Infrastructure.Data.Entities
{
    [DbCollection("leases")]
    public sealed class LeaseEntity : IDataEntity
    {
        [BsonId]
        public required string Id { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public required string Owner { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}

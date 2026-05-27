using Infrastructure.Data.Configuration;
using MongoDB.Bson.Serialization.Attributes;

namespace Infrastructure.Data.Entities
{
    [DbCollection("job_watermarks")]
    public sealed class JobWatermarkEntity : IDataEntity
    {
        [BsonId]
        public required string Id { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public DateTime Watermark { get; set; }
    }
}

using Data.Configuration;
using MongoDB.Bson.Serialization.Attributes;

namespace Data.Entities
{
    [DbCollection("job_watermarks")]
    public sealed class JobWatermarkEntity : IDataEntity
    {
        [BsonId]
        public required string Id { get; set; }

        public required string ETag { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public void OnSave()
        {
            throw new NotImplementedException();
        }

        [BsonElement("watermarkUtc")]
        public DateTime WatermarkUtc { get; set; }

        [BsonElement("updatedAtUtc")]
        public DateTime UpdatedAtUtc { get; set; }
    }
}

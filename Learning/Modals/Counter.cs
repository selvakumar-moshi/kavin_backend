using MongoDB.Bson.Serialization.Attributes;

namespace LearningBackendAPI.Models
{
    [BsonIgnoreExtraElements]
    public class Counter
    {
        [BsonId]
        public string Id { get; set; }

        [BsonElement("seq")]
        public long Seq { get; set; }
    }
}

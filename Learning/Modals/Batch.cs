using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LearningBackendAPI.Models
{
    [BsonIgnoreExtraElements]
    public class Batch
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("title")]
        public string Title { get; set; }

        [BsonElement("courseId")]
        public string CourseId { get; set; }

        [BsonElement("courseName")]
        public string CourseName { get; set; }

        [BsonElement("batchFrom")]
        public DateTime BatchFrom { get; set; }

        [BsonElement("batchTo")]
        public DateTime BatchTo { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [BsonIgnore]
        public bool IsExpired => BatchTo.Date < DateTime.UtcNow.Date;
    }
}

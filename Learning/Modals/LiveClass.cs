using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LearningBackendAPI.Models
{
    [BsonIgnoreExtraElements]
    public class LiveClass
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("title")]
        public string Title { get; set; } = string.Empty;

        [BsonElement("description")]
        public string? Description { get; set; }

        // Paid = only students with a verified enrollment in this course/batch can see it; Free = every student.
        // Live classes saved before this field existed have none and are treated as Free.
        [BsonElement("liveClassesType")]
        public string? LiveClassesType { get; set; }

        [BsonElement("courseId")]
        public string? CourseId { get; set; }

        [BsonElement("courseName")]
        public string? CourseName { get; set; }

        [BsonElement("batchId")]
        public string? BatchId { get; set; }

        [BsonElement("batchTitle")]
        public string? BatchTitle { get; set; }

        [BsonElement("youTubeLink")]
        public string? YouTubeLink { get; set; }

        [BsonElement("googleMeetLink")]
        public string? GoogleMeetLink { get; set; }

        // Calendar day of the class, "yyyy-MM-dd"
        [BsonElement("date")]
        public string Date { get; set; } = string.Empty;

        // 24 hour "HH:mm", in the live class time zone (see LiveClassService)
        [BsonElement("startTime")]
        public string StartTime { get; set; } = string.Empty;

        [BsonElement("endTime")]
        public string EndTime { get; set; } = string.Empty;

        // Not Started | Online | Expired. Worked out from date/startTime/endTime every time it is read,
        // so it is always right without a background job; the stored value is only a snapshot.
        [BsonElement("status")]
        public string Status { get; set; } = string.Empty;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
    }
}

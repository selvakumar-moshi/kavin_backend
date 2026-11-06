using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LearningBackendAPI.Models
{
    [BsonIgnoreExtraElements]
    public class Course
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("courseName")]
        public string CourseName { get; set; }

        [BsonElement("courseDescription")]
        public string CourseDescription { get; set; }

        [BsonElement("courseAmount")]
        public decimal CourseAmount { get; set; }

        // A course priced at 0 is a free course. Derived from the amount, so it can never disagree with it
        // (set the amount to 0 and the course is free; set it back above 0 and it is paid again)
        [BsonIgnore]
        public bool IsFree => CourseAmount == 0;

        // "Free" or "Paid", shown in the course list
        [BsonIgnore]
        public string CourseType => IsFree ? Utils.Constants.CourseTypes.Free : Utils.Constants.CourseTypes.Paid;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
    }
}
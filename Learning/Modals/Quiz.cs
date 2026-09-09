using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LearningBackendAPI.Models
{
    [BsonIgnoreExtraElements]
    public class QuizQuestionItem
    {
        [BsonElement("questionNumber")]
        public int QuestionNumber { get; set; }

        [BsonElement("questionText")]
        public string QuestionText { get; set; }

        [BsonElement("optionA")]
        public string OptionA { get; set; }

        [BsonElement("optionB")]
        public string OptionB { get; set; }

        [BsonElement("optionC")]
        public string OptionC { get; set; }

        [BsonElement("optionD")]
        public string OptionD { get; set; }

        [BsonElement("correctOption")]
        public string? CorrectOption { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class Quiz
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("courseId")]
        public string CourseId { get; set; }

        [BsonElement("courseName")]
        public string CourseName { get; set; }

        [BsonElement("title")]
        public string Title { get; set; }

        [BsonElement("status")]
        public string Status { get; set; }

        [BsonElement("publishedAt")]
        public DateTime? PublishedAt { get; set; }

        [BsonElement("questions")]
        public List<QuizQuestionItem> Questions { get; set; } = new();

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [BsonIgnore]
        public DateTime? ExpiresAt => PublishedAt?.AddHours(24);

        [BsonIgnore]
        public bool IsExpired => PublishedAt.HasValue && DateTime.UtcNow > PublishedAt.Value.AddHours(24);

        [BsonIgnore]
        public int? TimeLeftSeconds
        {
            get
            {
                if (!PublishedAt.HasValue)
                {
                    return null;
                }

                var remaining = PublishedAt.Value.AddHours(24) - DateTime.UtcNow;
                return remaining.TotalSeconds > 0 ? (int)remaining.TotalSeconds : 0;
            }
        }
    }
}

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

        [BsonElement("questionImageUrl")]
        public string? QuestionImageUrl { get; set; }

        [BsonElement("questionImageKey")]
        public string? QuestionImageKey { get; set; }

        [BsonElement("optionA")]
        public string OptionA { get; set; }

        [BsonElement("optionAImageUrl")]
        public string? OptionAImageUrl { get; set; }

        [BsonElement("optionAImageKey")]
        public string? OptionAImageKey { get; set; }

        [BsonElement("optionB")]
        public string OptionB { get; set; }

        [BsonElement("optionBImageUrl")]
        public string? OptionBImageUrl { get; set; }

        [BsonElement("optionBImageKey")]
        public string? OptionBImageKey { get; set; }

        [BsonElement("optionC")]
        public string OptionC { get; set; }

        [BsonElement("optionCImageUrl")]
        public string? OptionCImageUrl { get; set; }

        [BsonElement("optionCImageKey")]
        public string? OptionCImageKey { get; set; }

        [BsonElement("optionD")]
        public string OptionD { get; set; }

        [BsonElement("optionDImageUrl")]
        public string? OptionDImageUrl { get; set; }

        [BsonElement("optionDImageKey")]
        public string? OptionDImageKey { get; set; }

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

        [BsonElement("publishVersion")]
        public int PublishVersion { get; set; } = 0;

        [BsonElement("publishedAt")]
        public DateTime? PublishedAt { get; set; }

        [BsonElement("expiresAt")]
        public DateTime? ExpiresAt { get; set; }

        [BsonElement("questions")]
        public List<QuizQuestionItem> Questions { get; set; } = new();

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [BsonIgnore]
        public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

        [BsonIgnore]
        public int? TimeLeftSeconds
        {
            get
            {
                if (!ExpiresAt.HasValue)
                {
                    return null;
                }

                var remaining = ExpiresAt.Value - DateTime.UtcNow;
                return remaining.TotalSeconds > 0 ? (int)remaining.TotalSeconds : 0;
            }
        }
    }
}

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LearningBackendAPI.Models
{
    [BsonIgnoreExtraElements]
    public class QuizAnswerItem
    {
        [BsonElement("questionNumber")]
        public int QuestionNumber { get; set; }

        [BsonElement("selectedOption")]
        public string? SelectedOption { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class QuizAttempt
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("quizId")]
        public string QuizId { get; set; }

        [BsonElement("quizVersion")]
        public int QuizVersion { get; set; }

        [BsonElement("questionsSnapshot")]
        public List<QuizQuestionItem> QuestionsSnapshot { get; set; } = new();

        [BsonElement("userId")]
        public string UserId { get; set; }

        // The student's batch at the moment they submitted, so results stay attached to that
        // batch even if the quiz is later moved to a different one.
        [BsonElement("batchId")]
        public string? BatchId { get; set; }

        [BsonElement("batchTitle")]
        public string? BatchTitle { get; set; }

        [BsonElement("answers")]
        public List<QuizAnswerItem> Answers { get; set; } = new();

        [BsonElement("totalQuestions")]
        public int TotalQuestions { get; set; }

        [BsonElement("correctCount")]
        public int CorrectCount { get; set; }

        [BsonElement("wrongCount")]
        public int WrongCount { get; set; }

        [BsonElement("totalMarks")]
        public decimal TotalMarks { get; set; }

        [BsonElement("scoredMarks")]
        public decimal ScoredMarks { get; set; }

        [BsonElement("submittedAt")]
        public DateTime SubmittedAt { get; set; }
    }
}

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LearningBackendAPI.Models
{
    [BsonIgnoreExtraElements]
    public class QuizQuestionItem
    {
        [BsonElement("questionNumber")]
        public int QuestionNumber { get; set; }

        // Shared passage this question is based on (null = standalone question). Questions carrying the
        // same passage text are shown under one passage; each is still its own question and is counted
        // and marked individually.
        [BsonElement("passageQuiz")]
        public string? PassageQuiz { get; set; }

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

        [BsonElement("mark")]
        public decimal Mark { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class Quiz
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        // Empty for a Free quiz that was created without a course
        [BsonElement("courseId")]
        public string? CourseId { get; set; }

        [BsonElement("courseName")]
        public string? CourseName { get; set; }

        [BsonElement("batchId")]
        public string? BatchId { get; set; }

        [BsonElement("batchTitle")]
        public string? BatchTitle { get; set; }

        [BsonElement("title")]
        public string Title { get; set; }

        [BsonElement("description")]
        public string? Description { get; set; }

        [BsonElement("status")]
        public string Status { get; set; }

        // Most times one student may submit this quiz (per publish version); null on older quizzes = 1
        [BsonElement("quizAttempt")]
        public int? QuizAttempt { get; set; }

        // Marks deducted for each answered-but-wrong question (stored as a positive amount, e.g. 1.5); null or 0 = no negative marking
        [BsonElement("negativeMark")]
        public decimal? NegativeMark { get; set; }

        // The deduction applies once per this many wrong answers (e.g. 3 with NegativeMark 0.5 = 0.5 off for every 3 wrong). null = 1
        [BsonElement("wrongAnswersPerDeduction")]
        public int? WrongAnswersPerDeduction { get; set; }

        // Marks for each correct answer, applied to every question of the quiz (null = marks stay per question)
        [BsonElement("markPerQuestion")]
        public decimal? MarkPerQuestion { get; set; }

        // Computed on read (not stored), for the Configure Negative Marking screen
        [BsonIgnore]
        public int TotalQuestions => Questions.Count;

        [BsonIgnore]
        public decimal MaxMarks => Questions.Sum(q => q.Mark);

        // Name of the Word file the questions were imported from, e.g. "testQuiz.docx" (null for quizzes entered by hand)
        [BsonElement("sourceFileName")]
        public string? SourceFileName { get; set; }

        // The imported Word file itself, kept in S3 (coaching/question/document) so the admin can see which file the questions came from
        [BsonElement("sourceFileUrl")]
        public string? SourceFileUrl { get; set; }

        [BsonElement("sourceFileKey")]
        public string? SourceFileKey { get; set; }

        // competitive | school | previousYear | syllabusWise - which UI tab the quiz belongs to
        [BsonElement("quizType")]
        public string? QuizType { get; set; }

        // previousYear, syllabusWise and school quizzes: the folder (and sub folder, if the folder has any) the quiz is filed under
        [BsonElement("folderId")]
        public string? FolderId { get; set; }

        [BsonElement("folderName")]
        public string? FolderName { get; set; }

        [BsonElement("subFolderId")]
        public string? SubFolderId { get; set; }

        [BsonElement("subFolderName")]
        public string? SubFolderName { get; set; }

        // Computed on read (not stored): every sub folder from the folder's first level down to SubFolderName, joined by "/"
        [BsonIgnore]
        public string? SubFolderPath { get; set; }

        [BsonElement("quizToView")]
        public string QuizToView { get; set; }

        // Set at publish time: each student sees the questions in their own (stable) shuffled order
        [BsonElement("shuffleQuestions")]
        public bool ShuffleQuestions { get; set; }

        [BsonElement("publishVersion")]
        public int PublishVersion { get; set; } = 0;

        [BsonElement("publishedAt")]
        public DateTime? PublishedAt { get; set; }

        // Availability window, set at publish time, both UTC instants and both optional:
        // StartsAt null = available as soon as it is published; ExpiresAt null = never expires.
        [BsonElement("startsAt")]
        public DateTime? StartsAt { get; set; }

        [BsonElement("expiresAt")]
        public DateTime? ExpiresAt { get; set; }

        [BsonElement("questions")]
        public List<QuizQuestionItem> Questions { get; set; } = new();

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [BsonIgnore]
        public bool IsNotStarted => StartsAt.HasValue && DateTime.UtcNow < StartsAt.Value;

        // Not Started | Online | Expired for a published quiz (null for a draft). Worked out on every read, like live classes.
        [BsonIgnore]
        public string? ScheduleStatus => Status != "Published" ? null : IsExpired ? "Expired" : IsNotStarted ? "Not Started" : "Online";

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

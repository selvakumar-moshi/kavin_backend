namespace LearningBackendAPI.DTOs
{
    public class QuizStudentResponse
    {
        public string Id { get; set; } = "";
        public string? CourseId { get; set; }
        public string? CourseName { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string QuizToView { get; set; } = "";
        public string? QuizType { get; set; }
        public string? FolderId { get; set; }
        public string? FolderName { get; set; }
        public string? SubFolderId { get; set; }
        public string? SubFolderName { get; set; }
        // Every sub folder from the folder's first level down to SubFolderName, e.g. "Sub 1/Sub 1_1/Sub 1_2"
        public string? SubFolderPath { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsExpired { get; set; }
        // Attempts the admin allows each student, how many this student has used, and how many are left
        public int QuizAttempt { get; set; }
        public int AttemptsUsed { get; set; }
        public int AttemptsRemaining { get; set; }
        public int? TimeLeftSeconds { get; set; }
        public List<QuizStudentQuestionDto> Questions { get; set; } = new();
    }

    public class QuizStudentQuestionDto
    {
        // Fixed id of the question - this is what the answer is submitted against
        public int QuestionNumber { get; set; }
        // Position of the question in this student's order (1, 2, 3 ...) - show this as the question number
        public int DisplayNumber { get; set; }
        public string QuestionText { get; set; } = "";
        public string? QuestionImageUrl { get; set; }
        public string OptionA { get; set; } = "";
        public string? OptionAImageUrl { get; set; }
        public string OptionB { get; set; } = "";
        public string? OptionBImageUrl { get; set; }
        public string OptionC { get; set; } = "";
        public string? OptionCImageUrl { get; set; }
        public string? OptionD { get; set; }
        public string? OptionDImageUrl { get; set; }
        public decimal Mark { get; set; }
    }
}

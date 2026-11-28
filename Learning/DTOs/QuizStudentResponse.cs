namespace LearningBackendAPI.DTOs
{
    public class QuizStudentResponse : IFolderTrail
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
        // The pictures for the folder, the quiz's sub folder, and every level of SubFolderPath (first level down to the sub folder)
        public string? FolderImageUrl { get; set; }
        public string? SubFolderImageUrl { get; set; }
        public List<FolderNodeInfo>? SubFolderPathNodes { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        // When the quiz opens (null = open since it was published)
        public DateTime? StartsAt { get; set; }
        // Not Started | Online | Expired. While Not Started the questions are withheld and submitting is refused.
        public string? Status { get; set; }
        public bool IsNotStarted { get; set; }
        public bool IsExpired { get; set; }
        // Attempts the admin allows each student, how many this student has used, and how many are left
        public int QuizAttempt { get; set; }
        public int AttemptsUsed { get; set; }
        public int AttemptsRemaining { get; set; }
        // Marks deducted per wrong answer (0 = no negative marking)
        public decimal NegativeMark { get; set; }
        // NegativeMark is deducted once for every this-many wrong answers
        public int WrongAnswersPerDeduction { get; set; } = 1;
        public int? TimeLeftSeconds { get; set; }
        // Regular (non-passage) questions only
        public List<QuizStudentQuestionDto> Questions { get; set; } = new();
        // Passage blocks: each passage once, with all its questions together
        public List<QuizStudentPassageDto> PassageQuizzes { get; set; } = new();
    }

    public class QuizStudentPassageDto
    {
        public string PassageContent { get; set; } = "";
        // Position of the block's first question in this student's order (the block fills StartDisplayNumber ... + Questions.Count - 1)
        public int StartDisplayNumber { get; set; }
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

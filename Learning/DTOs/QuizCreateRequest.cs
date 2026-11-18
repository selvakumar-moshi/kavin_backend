namespace LearningBackendAPI.DTOs
{
    public class QuizCreateRequest
    {
        public string? CourseId { get; set; }
        public string? BatchId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        // Ignored for a "daily" quiz - it is always Free
        public string? QuizToView { get; set; }
        // Most times each student may submit the quiz (1 or more); defaults to 1
        public int? QuizAttempt { get; set; }
        // Marks deducted per wrong answer, e.g. -1.5 or 1.5 (both mean 1.5 off). Leave out or 0 for no negative marking
        public decimal? NegativeMark { get; set; }
        // Deduct NegativeMark once for every this-many wrong answers (1 or more; default 1). 3 + NegativeMark 0.5 = 0.5 off per 3 wrong
        public int? WrongAnswersPerDeduction { get; set; }
        // Marks for each correct answer, applied to every question (overrides per-question marks). Must be > 0
        public decimal? MarkPerQuestion { get; set; }
        // Required: "competitive", "school", "previousYear", "syllabusWise", "daily" or "currentAffairs" (the UI tab)
        public string? QuizType { get; set; }
        // Required for quizType "previousYear", "syllabusWise" and "school" (not allowed for other types): ids from GET /api/Folder.
        // SubFolderId is required when the chosen folder has sub folders.
        public string? FolderId { get; set; }
        public string? SubFolderId { get; set; }
        public List<QuizQuestionInput> Questions { get; set; } = new();

        // Optional Word (.docx) file: when passed, the questions are read from it instead of Questions
        public IFormFile? File { get; set; }

        // The Word (.docx) the questions were imported from (POST /api/Quiz/import), sent together with Questions so
        // the file is kept with the quiz. Not needed when File is used - that file is kept automatically.
        public IFormFile? SourceFile { get; set; }
    }
}

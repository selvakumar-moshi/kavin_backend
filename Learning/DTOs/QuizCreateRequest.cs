namespace LearningBackendAPI.DTOs
{
    public class QuizCreateRequest
    {
        public string? CourseId { get; set; }
        public string? BatchId { get; set; }
        public string? Title { get; set; }
        public string? QuizToView { get; set; }
        // Required: "competitive", "school" or "previousYear" (the UI tab)
        public string? QuizType { get; set; }
        // Optional classification for open (Free) quizzes: Subject -> Category (GK only) -> Standard -> Part (std 6/7 only).
        // GET /api/Quiz/categories returns the allowed combinations.
        public string? Subject { get; set; }
        public string? Category { get; set; }
        public int? Standard { get; set; }
        public string? Part { get; set; }
        // Required for quizType "previousYear" (not allowed for other types): ids from GET /api/Folder.
        // SubFolderId is required when the chosen folder has sub folders.
        public string? FolderId { get; set; }
        public string? SubFolderId { get; set; }
        public List<QuizQuestionInput> Questions { get; set; } = new();

        // Optional Word (.docx) file: when passed, the questions are read from it instead of Questions
        public IFormFile? File { get; set; }
    }
}

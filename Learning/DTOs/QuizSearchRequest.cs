namespace LearningBackendAPI.DTOs
{
    public class QuizSearchRequest
    {
        public string? CourseId { get; set; }
        public string? SearchTerm { get; set; }
        // Optional tab filter: "competitive", "school" or "previousYear"
        public string? QuizType { get; set; }
        // Optional access filter: "Free" or "Paid"
        public string? QuizToView { get; set; }
        // Optional filters on the quiz's classification
        public string? Subject { get; set; }
        public string? Category { get; set; }
        public int? Standard { get; set; }
        public string? Part { get; set; }
        // Optional filters for previousYear quizzes (ids from GET /api/Folder)
        public string? FolderId { get; set; }
        public string? SubFolderId { get; set; }
        public Dictionary<string, string>? GlobalFilter { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}

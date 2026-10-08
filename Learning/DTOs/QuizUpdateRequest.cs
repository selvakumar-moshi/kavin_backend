namespace LearningBackendAPI.DTOs
{
    public class QuizUpdateRequest
    {
        public string? BatchId { get; set; }
        public string? Title { get; set; }
        public string? QuizToView { get; set; }
        public string? QuizType { get; set; }
        // Pass Subject to replace the quiz's whole classification (Subject/Category/Standard/Part);
        // leave Subject out to keep it as it is
        // Optional classification for open (Free) quizzes: Subject -> Category (GK only) -> Standard -> Part (std 6/7 only).
        // GET /api/Quiz/categories returns the allowed combinations.
        public string? Subject { get; set; }
        public string? Category { get; set; }
        public int? Standard { get; set; }
        public string? Part { get; set; }
        // previousYear quizzes only. Pass FolderId (+ SubFolderId if the folder has sub folders) to replace the
        // quiz's folder; leave FolderId out to keep it. Switching a quiz to previousYear needs a folder.
        public string? FolderId { get; set; }
        public string? SubFolderId { get; set; }
        public List<QuizQuestionInput>? Questions { get; set; }
    }
}

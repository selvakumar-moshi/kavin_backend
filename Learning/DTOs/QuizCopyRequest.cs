namespace LearningBackendAPI.DTOs
{
    public class QuizCopyRequest
    {
        public string? QuizId { get; set; }
        // "Paid" or "Free" for the copy (defaults to the original's). Paid needs a batchId; Free doesn't.
        public string? QuizToView { get; set; }
        public string? BatchId { get; set; }
        // Only needed when copying a course-less Free quiz into a Paid quiz
        public string? CourseId { get; set; }
        public string? Title { get; set; }
    }
}

namespace LearningBackendAPI.DTOs
{
    public class QuizCopyRequest
    {
        public string? QuizId { get; set; }
        public string? BatchId { get; set; }
        public string? Title { get; set; }
    }
}

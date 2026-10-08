namespace LearningBackendAPI.DTOs
{
    public class RankListRequest
    {
        // Optional: a batch id of the course, or "all" for every batch (Paid view only)
        public string? BatchId { get; set; }
        // Optional check that the quiz is of this type: "competitive", "school" or "previousYear"
        public string? QuizType { get; set; }
        // Optional: "Paid" ranks students with a verified paid enrollment, "Free" ranks students without one.
        // Defaults to the quiz's own access.
        public string? QuizToView { get; set; }
    }
}

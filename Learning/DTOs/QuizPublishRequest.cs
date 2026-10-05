namespace LearningBackendAPI.DTOs
{
    public class QuizPublishRequest
    {
        public DateTime ExpiresAt { get; set; }

        // Show each student the questions in a different order (default: true)
        public bool? ShuffleQuestions { get; set; }
    }
}

namespace LearningBackendAPI.DTOs
{
    public class QuizUpdateRequest
    {
        public string? Title { get; set; }
        public List<QuizQuestionInput>? Questions { get; set; }
    }
}

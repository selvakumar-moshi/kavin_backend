namespace LearningBackendAPI.DTOs
{
    public class QuizCreateRequest
    {
        public string? CourseId { get; set; }
        public string? Title { get; set; }
        public string? QuizToView { get; set; }
        public List<QuizQuestionInput> Questions { get; set; } = new();
    }
}

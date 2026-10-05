namespace LearningBackendAPI.DTOs
{
    public class QuizCreateRequest
    {
        public string? CourseId { get; set; }
        public string? BatchId { get; set; }
        public string? Title { get; set; }
        public string? QuizToView { get; set; }
        public List<QuizQuestionInput> Questions { get; set; } = new();

        // Optional Word (.docx) file: when passed, the questions are read from it instead of Questions
        public IFormFile? File { get; set; }
    }
}

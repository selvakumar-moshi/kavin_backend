namespace LearningBackendAPI.DTOs
{
    public class QuizSearchRequest
    {
        public string? CourseId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}

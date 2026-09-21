namespace LearningBackendAPI.DTOs
{
    public class QuizStudentResponse
    {
        public string Id { get; set; } = "";
        public string CourseId { get; set; } = "";
        public string CourseName { get; set; } = "";
        public string Title { get; set; } = "";
        public DateTime? PublishedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsExpired { get; set; }
        public int? TimeLeftSeconds { get; set; }
        public List<QuizStudentQuestionDto> Questions { get; set; } = new();
    }

    public class QuizStudentQuestionDto
    {
        public int QuestionNumber { get; set; }
        public string QuestionText { get; set; } = "";
        public string? QuestionImageUrl { get; set; }
        public string OptionA { get; set; } = "";
        public string? OptionAImageUrl { get; set; }
        public string OptionB { get; set; } = "";
        public string? OptionBImageUrl { get; set; }
        public string OptionC { get; set; } = "";
        public string? OptionCImageUrl { get; set; }
        public string OptionD { get; set; } = "";
        public string? OptionDImageUrl { get; set; }
    }
}

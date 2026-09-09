namespace LearningBackendAPI.DTOs
{
    public class QuizSubmitRequest
    {
        public List<QuizAnswerSubmitItem> Answers { get; set; } = new();
    }

    public class QuizAnswerSubmitItem
    {
        public int QuestionNumber { get; set; }
        public string? SelectedOption { get; set; }
    }

    public class QuizResultResponse
    {
        public string QuizId { get; set; } = "";
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public string Score { get; set; } = "";
        public DateTime SubmittedAt { get; set; }
        public List<QuizResultQuestionDto> Questions { get; set; } = new();
    }

    public class QuizResultQuestionDto
    {
        public int QuestionNumber { get; set; }
        public string QuestionText { get; set; } = "";
        public string OptionA { get; set; } = "";
        public string OptionB { get; set; } = "";
        public string OptionC { get; set; } = "";
        public string OptionD { get; set; } = "";
        public string? SelectedOption { get; set; }
        public string? CorrectOption { get; set; }
        public bool IsCorrect { get; set; }
    }
}

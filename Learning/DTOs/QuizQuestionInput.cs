namespace LearningBackendAPI.DTOs
{
    public class QuizQuestionInput
    {
        public string QuestionText { get; set; } = "";
        public IFormFile? QuestionImage { get; set; }
        public string OptionA { get; set; } = "";
        public IFormFile? OptionAImage { get; set; }
        public string OptionB { get; set; } = "";
        public IFormFile? OptionBImage { get; set; }
        public string OptionC { get; set; } = "";
        public IFormFile? OptionCImage { get; set; }
        public string OptionD { get; set; } = "";
        public IFormFile? OptionDImage { get; set; }
        public string? CorrectOption { get; set; }
    }
}

namespace LearningBackendAPI.DTOs
{
    // The question and each of options A-C need text OR an image (checked in QuizService); option D is optional.
    // The text fields are nullable so a question given as an image alone isn't rejected as "field is required".
    public class QuizQuestionInput
    {
        public string? QuestionText { get; set; }
        public IFormFile? QuestionImage { get; set; }
        public string? OptionA { get; set; }
        public IFormFile? OptionAImage { get; set; }
        public string? OptionB { get; set; }
        public IFormFile? OptionBImage { get; set; }
        public string? OptionC { get; set; }
        public IFormFile? OptionCImage { get; set; }
        public string? OptionD { get; set; }
        public IFormFile? OptionDImage { get; set; }
        // Update only: true deletes the image already saved for that slot (when no new image is sent to replace it).
        // Omitting an image on update keeps the saved one, so removal has to be explicit.
        public bool? RemoveQuestionImage { get; set; }
        public bool? RemoveOptionAImage { get; set; }
        public bool? RemoveOptionBImage { get; set; }
        public bool? RemoveOptionCImage { get; set; }
        public bool? RemoveOptionDImage { get; set; }
        public string? CorrectOption { get; set; }
        public decimal? Mark { get; set; }
    }
}

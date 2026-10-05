namespace LearningBackendAPI.DTOs
{
    // POST /api/Quiz/import - just the document; nothing is saved
    public class QuizImportRequest
    {
        public IFormFile? File { get; set; }
    }

    public class QuizImportIssueDto
    {
        // The question's number as written in the Word document
        public int DocumentNumber { get; set; }
        public string Reason { get; set; } = "";
    }

    public class QuizImportQuestionDto
    {
        public int DocumentNumber { get; set; }
        public string QuestionText { get; set; } = "";
        public string OptionA { get; set; } = "";
        public string OptionB { get; set; } = "";
        public string OptionC { get; set; } = "";
        public string OptionD { get; set; } = "";
        public string CorrectOption { get; set; } = "";
    }

    public class QuizImportPreviewResponse
    {
        public int TotalFoundInDocument { get; set; }
        public int Readable { get; set; }
        public List<QuizImportQuestionDto> Questions { get; set; } = new();
        public List<QuizImportIssueDto> Skipped { get; set; } = new();
        public List<QuizImportIssueDto> Warnings { get; set; } = new();
    }
}

namespace LearningBackendAPI.DTOs
{
    public class QuizUpdateRequest
    {
        public string? BatchId { get; set; }
        public string? Title { get; set; }
        // Leave out to keep it; send "" to clear it
        public string? Description { get; set; }
        public string? QuizToView { get; set; }
        public string? QuizType { get; set; }
        // Most times each student may submit the quiz (1 or more); leave out to keep it
        public int? QuizAttempt { get; set; }
        // Marks deducted per wrong answer, e.g. -1.5 or 1.5; leave out to keep it, send 0 to turn negative marking off
        public decimal? NegativeMark { get; set; }
        // Deduct NegativeMark once for every this-many wrong answers (1 or more); leave out to keep
        public int? WrongAnswersPerDeduction { get; set; }
        // Marks for each correct answer, applied to every question (overrides per-question marks); leave out to keep. Must be > 0
        public decimal? MarkPerQuestion { get; set; }
        // previousYear, syllabusWise and school quizzes only. Pass FolderId (+ SubFolderId if the folder has sub folders) to replace the
        // quiz's folder; leave FolderId out to keep it. Switching a quiz to previousYear, syllabusWise or school needs a folder.
        public string? FolderId { get; set; }
        public string? SubFolderId { get; set; }
        public List<QuizQuestionInput>? Questions { get; set; }
        // A replacement Word (.docx) the new Questions were imported from: it is kept with the quiz and the old file is removed
        public IFormFile? SourceFile { get; set; }
        // true removes the quiz's saved Word file (when no SourceFile is sent to replace it)
        public bool? RemoveSourceFile { get; set; }
    }
}

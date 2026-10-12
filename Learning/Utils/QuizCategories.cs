namespace LearningBackendAPI.Utils
{
    // A quiz's Subject → (Category) → Standard → (Part) selection. The valid options are managed
    // through QuizStructureService / the /api/QuizStructure endpoints.
    public class QuizClassification
    {
        public string? QuizType { get; set; }
        public string? QuizToView { get; set; }
        public string? Subject { get; set; }
        public string? Category { get; set; }
        public int? Standard { get; set; }
        public string? Part { get; set; }
        public string? FolderId { get; set; }
        public string? SubFolderId { get; set; }
        // Filled in by the service: SubFolderId plus every sub folder below it, so a middle level matches its leaves
        public HashSet<string>? SubFolderIds { get; set; }

        public bool IsEmpty => QuizType == null && QuizToView == null && Subject == null && Category == null && Standard == null && Part == null
            && FolderId == null && SubFolderId == null;
    }
}

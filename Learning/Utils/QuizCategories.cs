namespace LearningBackendAPI.Utils
{
    // The filters the quiz list accepts: type, access and folder / sub folder
    public class QuizClassification
    {
        public string? QuizType { get; set; }
        public string? QuizToView { get; set; }
        public string? FolderId { get; set; }
        public string? SubFolderId { get; set; }
        // Filled in by the service: SubFolderId plus every sub folder below it, so a middle level matches its leaves
        public HashSet<string>? SubFolderIds { get; set; }

        public bool IsEmpty => QuizType == null && QuizToView == null
            && FolderId == null && SubFolderId == null;
    }
}

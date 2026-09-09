namespace LearningBackendAPI.DTOs
{
    public class BatchCreateRequest
    {
        public string? Title { get; set; }
        public string? CourseId { get; set; }
        public DateTime? BatchFrom { get; set; }
        public DateTime? BatchTo { get; set; }
    }

    public class BatchUpdateRequest
    {
        public string? Title { get; set; }
        public string? CourseId { get; set; }
        public DateTime? BatchFrom { get; set; }
        public DateTime? BatchTo { get; set; }
    }
}

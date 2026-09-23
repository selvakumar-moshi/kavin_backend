namespace LearningBackendAPI.DTOs
{
    public class FreeMaterialDto
    {
        public string Id { get; set; } = "";
        public string MaterialType { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string CourseId { get; set; } = "";
        public string? BatchId { get; set; }
        public string? BatchTitle { get; set; }
        public string? PdfUrl { get; set; }
        public string? YoutubeLink { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

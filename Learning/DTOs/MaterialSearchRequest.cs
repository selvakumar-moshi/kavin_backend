namespace LearningBackendAPI.DTOs
{
    public class MaterialSearchRequest
    {
        public string? CourseId { get; set; }
        public string? Material { get; set; }
        public string? SearchTerm { get; set; }
        public Dictionary<string, string>? GlobalFilter { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}

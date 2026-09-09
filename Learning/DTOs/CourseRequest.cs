namespace LearningBackendAPI.DTOs
{
    public class CourseRequest
    {
        public string? CourseName { get; set; }
        public string? CourseDescription { get; set; }
        public decimal? CourseAmount { get; set; }
    }
}
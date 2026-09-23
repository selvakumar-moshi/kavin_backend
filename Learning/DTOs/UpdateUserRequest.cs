namespace LearningBackendAPI.DTOs
{
    public class UpdateUserRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? District { get; set; }
        public List<CourseEnrollmentRequest>? Courses { get; set; }
    }

    public class CourseEnrollmentRequest
    {
        public string CourseId { get; set; } = string.Empty;
        public string BatchId { get; set; } = string.Empty;
    }
}

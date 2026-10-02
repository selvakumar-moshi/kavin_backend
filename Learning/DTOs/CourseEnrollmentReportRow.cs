namespace LearningBackendAPI.DTOs
{
    public class CourseEnrollmentReportRow
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string? ApplicationNo { get; set; }
        public string Email { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string? District { get; set; }
        public string? BatchTitle { get; set; }
        public string Status { get; set; } = "";
    }
}

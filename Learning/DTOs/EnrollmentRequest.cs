namespace LearningBackendAPI.DTOs
{
    public class UpdateEnrollmentStatusRequest
    {
        public string? Status { get; set; }
        public string? PaymentMethod { get; set; }
        public string? TransactionReference { get; set; }
    }

    public class EnrollRequest
    {
        public string? CourseId { get; set; }
        public string? BatchId { get; set; }
    }
}

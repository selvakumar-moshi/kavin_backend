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

    // POST /api/Enrollment/{id}/payment-attachment (multipart/form-data)
    public class PaymentAttachmentRequest
    {
        // GPay / PhonePe / UPI screenshot (JPG, PNG or WEBP, up to 5 MB)
        public IFormFile? File { get; set; }
        // The UPI transaction ID / UTR shown in the payment app (6-35 letters or digits). An ID already used for another payment is refused.
        public string? TransactionId { get; set; }
    }

    public class PaymentInfoResponse
    {
        public string UpiId { get; set; } = "";
        public string PayeeName { get; set; } = "";
    }
}

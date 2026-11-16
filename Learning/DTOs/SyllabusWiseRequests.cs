namespace LearningBackendAPI.DTOs
{
    public class SyllabusWisePlanRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public decimal? Amount { get; set; }
        // A folder or sub folder id from GET /api/Folder: one price for it and everything nested below it.
        // Leave it empty for a plan that covers every syllabusWise quiz. On update, send "" to clear it.
        public string? FolderId { get; set; }
    }

    // POST /api/SyllabusWise/payment (multipart/form-data) - the student uploads the screenshot of their payment.
    // The payment is always created as Pending; only the admin can verify or reject it.
    public class SyllabusWisePaymentRequest
    {
        // Optional when exactly one plan exists
        public string? PlanId { get; set; }
        // GPay / PhonePe / UPI screenshot (JPG, PNG or WEBP, up to 5 MB)
        public IFormFile? File { get; set; }
        public string? PaymentMethod { get; set; }
        public string? TransactionReference { get; set; }
    }

    // PUT /api/SyllabusWise/payments/{id}/status - admin
    public class SyllabusWisePaymentStatusRequest
    {
        public string? PaymentStatus { get; set; }
    }

    public class SyllabusWisePaymentDto
    {
        public string PaymentId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string PlanId { get; set; } = string.Empty;
        public string PlanTitle { get; set; } = string.Empty;
        public string? FolderId { get; set; }
        public decimal Amount { get; set; }
        public string? PaymentMethod { get; set; }
        public string? TransactionReference { get; set; }
        public string? PaymentScreenshot { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime? VerifiedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

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

    // POST /api/SyllabusWise/payment - sent by the student after paying
    public class SyllabusWisePaymentRequest
    {
        // "verified", "pending" or "rejected" (any case)
        public string? PaymentStatus { get; set; }
        // Optional. The payer is always the signed-in student; if this is sent it must be that student's own id
        public string? UserId { get; set; }
        // Optional when exactly one plan exists
        public string? PlanId { get; set; }
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
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime? VerifiedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

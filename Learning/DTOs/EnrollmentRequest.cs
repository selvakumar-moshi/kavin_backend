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

    /// <summary>
    /// Sent by the mobile app after the student returns from their UPI app. The UPI app's reply is
    /// produced on the phone and can be faked, so this never verifies anything - it only records the
    /// transaction ID for the admin to match against the bank statement.
    /// </summary>
    public class UpiPaymentRequest
    {
        public string? TransactionReference { get; set; }
        /// <summary>Status the UPI app reported (SUCCESS / SUBMITTED / FAILURE / empty). Informational only.</summary>
        public string? UpiStatus { get; set; }
    }

    public class PaymentInfoResponse
    {
        public string UpiId { get; set; } = "";
        public string PayeeName { get; set; } = "";
    }
}

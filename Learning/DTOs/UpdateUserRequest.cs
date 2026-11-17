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
        // Left out or "add": the student joins this batch IN ADDITION to the batch(es) they already have - it gets its own
        // enrollment with its own amount (the course price on the day it is added), payment and status. Nothing existing changes.
        // "transfer": the new batch REPLACES one of theirs. The old batch stays in the list as Dropped, for history.
        public string? Mode { get; set; }
        // transfer only, when the student is in several batches of the course: the batch being replaced
        public string? FromBatchId { get; set; }
        // transfer: false / left out = free transfer, true = pay again. add: left out / true = the student pays for the new batch
        // (Pending until verified), false = admin grants it free (Verified).
        // Only used when this moves a student who is already in the course to another batch (the new batch replaces the old one).
        // false / left out: free transfer - the student stays Verified and keeps what they paid.
        // true: the student must pay for the new batch - the enrollment goes back to Pending (batch content locks) until they upload
        // a payment screenshot and the admin verifies it.
        public bool? PaymentRequired { get; set; }
        // Why the student is being moved (kept on the enrollment for reference)
        public string? Reason { get; set; }
    }
}

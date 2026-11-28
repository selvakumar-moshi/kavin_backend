using System.Text.Json.Serialization;
using LearningBackendAPI.Models;

namespace LearningBackendAPI.DTOs
{
    public class UserProfileResponse
    {
        [JsonPropertyName("userId")]
        public string Id { get; set; }
        public string? ApplicationNo { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string? District { get; set; }
        public string Role { get; set; }
        public string ProfileImage { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        // Admin view only (GET /api/User/{id}); null on /me. SavedDeviceId is the SHA-256 hash of the registered device id
        public string? SavedDeviceId { get; set; }
        public DateTime? DeviceRegisteredAt { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public List<EnrolledCourseDto> Courses { get; set; } = new();
        // Syllabus wise quiz payments (newest first) - lets the admin track each student's payment status
        public List<SyllabusWisePaymentDto> SyllabusWisePayments { get; set; } = new();
        // The syllabus PDFs those payments unlock (each with its folder, subFolderPath and pdfUrl)
        public List<SyllabusMaterial> SyllabusMaterials { get; set; } = new();
    }

    // One entry per course; every batch the student joined is its own entry in Batches, each with its own
    // amount, payment and status, so a later price change never touches what an earlier batch was bought for.
    public class EnrolledCourseDto
    {
        public string CourseId { get; set; }
        public string CourseName { get; set; }
        public List<EnrolledBatchDto> Batches { get; set; } = new();

        // Batches of this course the student can still be added to or moved to (admin view only): not ended, and not
        // one they are already in. Pass one as batchId to PUT /api/User/{id}.
        public List<AllowedBatchDto>? AllowedBatches { get; set; }
    }

    public class EnrolledBatchDto
    {
        public string EnrollmentId { get; set; }
        public string? BatchId { get; set; }
        public string? BatchTitle { get; set; }
        // The batch's PDF, if the admin attached one
        public string? BatchPdfUrl { get; set; }
        public string? BatchPdfName { get; set; }
        // What this batch was bought for (the course price on the day it was added)
        public decimal Amount { get; set; }
        public string? PaymentMethod { get; set; }
        public string? TransactionReference { get; set; }
        public string? PaymentScreenshot { get; set; }
        public string EnrollmentStatus { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public DateTime EnrolledAt { get; set; }
        // Set when the admin moved the student into this batch from another one
        public string? TransferredFromBatchId { get; set; }
        public string? TransferredFromBatchTitle { get; set; }
        public string? TransferReason { get; set; }
        public DateTime? TransferredAt { get; set; }
        // Only filled while the enrollment is Verified: this batch's content (plus the course content that has no batch)
        public List<StudyMaterial> StudyMaterials { get; set; } = new();
        public List<QuestionPaper> QuestionPapers { get; set; } = new();
        public List<VideoMaterial> VideoMaterials { get; set; } = new();
    }

    public class AllowedBatchDto
    {
        public string BatchId { get; set; }
        public string Title { get; set; }
        public DateTime BatchFrom { get; set; }
        public DateTime BatchTo { get; set; }
    }
}

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
        public List<EnrolledCourseDto> Courses { get; set; } = new();
    }

    public class EnrolledCourseDto
    {
        public string EnrollmentId { get; set; }
        public string CourseId { get; set; }
        public string CourseName { get; set; }
        public string? BatchId { get; set; }
        public string? BatchTitle { get; set; }
        public decimal CourseAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string? PaymentMethod { get; set; }
        public string? TransactionReference { get; set; }
        public string? PaymentScreenshot { get; set; }
        public string EnrollmentStatus { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public List<StudyMaterial> StudyMaterials { get; set; } = new();
        public List<VideoMaterial> VideoMaterials { get; set; } = new();

        // Batches of this course the student can be moved to (admin view only): not ended, and not
        // the batch they are already in. Pass one as batchId to PUT /api/User/{id}.
        public List<AllowedBatchDto>? AllowedBatches { get; set; }
    }

    public class AllowedBatchDto
    {
        public string BatchId { get; set; }
        public string Title { get; set; }
        public DateTime BatchFrom { get; set; }
        public DateTime BatchTo { get; set; }
    }
}

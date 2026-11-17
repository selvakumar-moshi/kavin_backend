using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LearningBackendAPI.Models
{
    [BsonIgnoreExtraElements]
    public class Enrollment
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        [JsonPropertyName("enrollmentId")]
        public string Id { get; set; }

        [BsonElement("userId")]
        public string UserId { get; set; }

        [BsonElement("courseId")]
        public string CourseId { get; set; }

        [BsonElement("courseName")]
        public string CourseName { get; set; }

        [BsonElement("batchId")]
        public string? BatchId { get; set; }

        [BsonElement("batchTitle")]
        public string? BatchTitle { get; set; }

        [BsonElement("courseAmount")]
        public decimal CourseAmount { get; set; }

        [BsonElement("totalAmount")]
        public decimal TotalAmount { get; set; }

        [BsonElement("paymentMethod")]
        public string? PaymentMethod { get; set; }

        [BsonElement("transactionReference")]
        public string? TransactionReference { get; set; }

        [BsonElement("paymentScreenshot")]
        public string? PaymentScreenshot { get; set; }

        [BsonElement("paymentScreenshotKey")]
        [JsonIgnore]
        public string? PaymentScreenshotKey { get; set; }

        [BsonElement("status")]
        public string Status { get; set; }

        [BsonElement("verifiedAt")]
        public DateTime? VerifiedAt { get; set; }

        [BsonElement("verifiedByAdminId")]
        public string? VerifiedByAdminId { get; set; }

        // Set when the admin moves the student from one batch to another (the new batch replaces the old one)
        [BsonElement("transferredFromBatchId")]
        public string? TransferredFromBatchId { get; set; }

        [BsonElement("transferredFromBatchTitle")]
        public string? TransferredFromBatchTitle { get; set; }

        [BsonElement("transferReason")]
        public string? TransferReason { get; set; }

        [BsonElement("transferredAt")]
        public DateTime? TransferredAt { get; set; }

        [BsonElement("transferredByAdminId")]
        public string? TransferredByAdminId { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
    }
}

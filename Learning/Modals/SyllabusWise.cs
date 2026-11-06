using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LearningBackendAPI.Models
{
    // What a student buys to unlock the paid "syllabusWise" quizzes (managed by the admin)
    [BsonIgnoreExtraElements]
    public class SyllabusWisePlan
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("title")]
        public string Title { get; set; } = string.Empty;

        [BsonElement("description")]
        public string? Description { get; set; }

        [BsonElement("amount")]
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Amount { get; set; }

        // A folder or sub folder (any level) from GET /api/Folder: the plan covers it and everything nested below it.
        // Empty means the plan covers every syllabusWise quiz.
        [BsonElement("folderId")]
        public string? FolderId { get; set; }

        // Looked up on read, so a renamed folder shows its new name
        [BsonIgnore]
        public string? FolderName { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
    }

    // A student's payment for a plan. One record per student and plan - sending it again updates it.
    [BsonIgnoreExtraElements]
    public class SyllabusWisePayment
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("userId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("planId")]
        public string PlanId { get; set; } = string.Empty;

        // Copied from the plan when the payment is made, so later plan edits don't rewrite what was paid
        [BsonElement("planTitle")]
        public string PlanTitle { get; set; } = string.Empty;

        [BsonElement("amount")]
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Amount { get; set; }

        // The folder the plan covered when it was paid for (empty = every syllabusWise quiz)
        [BsonElement("folderId")]
        public string? FolderId { get; set; }

        [BsonElement("paymentMethod")]
        public string? PaymentMethod { get; set; }

        [BsonElement("transactionReference")]
        public string? TransactionReference { get; set; }

        [BsonElement("paymentStatus")]
        public string PaymentStatus { get; set; } = string.Empty;

        [BsonElement("verifiedAt")]
        public DateTime? VerifiedAt { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
    }
}

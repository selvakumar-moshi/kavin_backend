using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LearningBackendAPI.Models
{
    [BsonIgnoreExtraElements]
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("firstName")]
        public string FirstName { get; set; }

        [BsonElement("lastName")]
        public string LastName { get; set; }

        [BsonElement("email")]
        public string Email { get; set; }

        [BsonElement("applicationNo")]
        public string? ApplicationNo { get; set; }

        [BsonElement("phoneNumber")]
        public string PhoneNumber { get; set; }

        [BsonElement("district")]
        public string? District { get; set; }

        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; }

        [BsonElement("profileImage")]
        public string? ProfileImage { get; set; }

        [BsonElement("profileImageKey")]
        public string? ProfileImageKey { get; set; }

        // True after an admin reset: the student must replace the temporary password before using the app
        [BsonElement("mustChangePassword")]
        public bool MustChangePassword { get; set; }

        // SHA-256 hash of the device id the student first logged in from (null = no device registered yet).
        // Only admin reset clears it. The raw id is never stored.
        [BsonElement("savedDeviceId")]
        public string? SavedDeviceId { get; set; }

        [BsonElement("deviceRegisteredAt")]
        public DateTime? DeviceRegisteredAt { get; set; }

        [BsonElement("lastLoginDate")]
        public DateTime? LastLoginDate { get; set; }

        // When the student last uploaded a payment screenshot (course enrollment or syllabus wise), kept here so the admin
        // user table can show and filter by it. Set on every upload; null = never uploaded one.
        [BsonElement("paymentUploadedAt")]
        public DateTime? PaymentUploadedAt { get; set; }

        [BsonElement("role")]
        public string Role { get; set; } = "User";

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
    }
}
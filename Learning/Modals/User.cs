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

        [BsonElement("role")]
        public string Role { get; set; } = "User";

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
    }
}
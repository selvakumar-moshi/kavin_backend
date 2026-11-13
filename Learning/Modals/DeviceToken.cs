using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LearningBackendAPI.Models
{
    // An FCM registration token of one device a student is logged in on. A token belongs to one user at a time.
    [BsonIgnoreExtraElements]
    public class DeviceToken
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("userId")]
        public string UserId { get; set; }

        [BsonElement("token")]
        public string Token { get; set; }

        [BsonElement("platform")]
        public string? Platform { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("lastSeenAt")]
        public DateTime LastSeenAt { get; set; }
    }
}

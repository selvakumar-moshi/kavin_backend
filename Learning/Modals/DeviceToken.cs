using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LearningBackendAPI.Models
{
    // A user's current FCM registration token - one record per user, replaced when they log in again or Firebase refreshes the token.
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

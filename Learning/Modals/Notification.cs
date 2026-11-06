using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LearningBackendAPI.Models
{
    [BsonIgnoreExtraElements]
    public class Notification
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("title")]
        public string? Title { get; set; }

        [BsonElement("description")]
        public string? Description { get; set; }

        [BsonElement("link")]
        public string? Link { get; set; }

        // "Flash Notification" or "Job Notification". Notifications saved before types existed (or as "Push Notification") are Flash.
        [BsonElement("notificationType")]
        public string NotificationType { get; set; } = Utils.Constants.NotificationTypes.Flash;

        // StartDate and EndDate are both optional
        [BsonElement("startDate")]
        public DateTime? StartDate { get; set; }

        [BsonElement("endDate")]
        public DateTime? EndDate { get; set; }

        // Optional attached file (PDF or image) - uploaded with the create / update request
        [BsonElement("attachmentUrl")]
        public string? AttachmentUrl { get; set; }

        [BsonElement("attachmentName")]
        public string? AttachmentName { get; set; }

        [BsonElement("attachmentKey")]
        [JsonIgnore]
        public string? AttachmentKey { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
    }
}

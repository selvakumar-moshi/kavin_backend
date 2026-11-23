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

        // Flash Notifications only: "Paid" (only students who bought the course / batch) or "Free" (everyone). It is a shortcut for
        // the audience: Paid sets audienceType to Batch (batchId given) or Course, Free sets it to All. Null on Job Notifications
        // and on notifications saved before it existed.
        [BsonElement("courseType")]
        public string? CourseType { get; set; }

        // Who the notification is for: All (default, also for notifications saved before audiences existed), Course, Batch,
        // Users (specific students) or SyllabusPaid (students with a verified syllabusWise payment). It filters the in-app
        // list for students and is the audience of the push.
        [BsonElement("audienceType")]
        public string AudienceType { get; set; } = Utils.Constants.NotificationAudiences.All;

        [BsonElement("courseId")]
        public string? CourseId { get; set; }

        [BsonElement("batchId")]
        public string? BatchId { get; set; }

        [BsonElement("userIds")]
        public List<string>? UserIds { get; set; }

        // Push delivery. SendPush is separate from NotificationType: a Flash or a Job notification can be pushed or not.
        [BsonElement("sendPush")]
        public bool SendPush { get; set; }

        // When the push goes out (UTC); a push without a schedule is queued for "now"
        [BsonElement("pushScheduledAt")]
        public DateTime? PushScheduledAt { get; set; }

        // None, Scheduled, Sending, Sent, PartiallyFailed, Failed or Cancelled
        [BsonElement("pushStatus")]
        public string PushStatus { get; set; } = Utils.Constants.PushStatuses.None;

        [BsonElement("pushClaimedAt")]
        [JsonIgnore]
        public DateTime? PushClaimedAt { get; set; }

        [BsonElement("pushSentAt")]
        public DateTime? PushSentAt { get; set; }

        // Devices targeted / accepted by FCM / rejected. "Accepted" means FCM took the message, not that it was seen.
        [BsonElement("pushTargetCount")]
        public int PushTargetCount { get; set; }

        [BsonElement("pushSuccessCount")]
        public int PushSuccessCount { get; set; }

        [BsonElement("pushFailureCount")]
        public int PushFailureCount { get; set; }

        [BsonElement("pushError")]
        public string? PushError { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
    }
}

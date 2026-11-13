namespace LearningBackendAPI.DTOs
{
    public class NotificationCreateRequest
    {
        // Every field is optional. NotificationType: "Flash Notification" (default) or "Job Notification"
        public string? NotificationType { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Link { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        // Optional attachment: a PDF, or a JPG / PNG / WEBP image, up to 10 MB
        public IFormFile? File { get; set; }

        // Audience: All (default), Course (courseId), Batch (batchId) , Users (userIds) or SyllabusPaid
        public string? AudienceType { get; set; }
        public string? CourseId { get; set; }
        public string? BatchId { get; set; }
        public List<string>? UserIds { get; set; }

        // true also sends a push to the audience's devices. PushScheduledAt (send an ISO time with Z or an offset)
        // delays it; without it the push goes out within seconds of saving.
        public bool? SendPush { get; set; }
        public DateTime? PushScheduledAt { get; set; }
    }

    public class NotificationUpdateRequest
    {
        public string? NotificationType { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Link { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        // Passing a file replaces the current attachment
        public IFormFile? File { get; set; }
        // true removes the current attachment (ignored when a new File is passed)
        public bool? RemoveFile { get; set; }

        // Audience and push settings; only a push that hasn't gone out yet can be changed - use
        // POST /api/Notification/{id}/push to send again
        public string? AudienceType { get; set; }
        public string? CourseId { get; set; }
        public string? BatchId { get; set; }
        public List<string>? UserIds { get; set; }
        public bool? SendPush { get; set; }
        public DateTime? PushScheduledAt { get; set; }
    }
}

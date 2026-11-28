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

        // Flash Notification only (ignored for Job Notifications): "Paid" = only students who bought courseId (and batchId, if sent);
        // "Free" = everyone. Paid needs courseId. It decides the audience, so audienceType may be left out or must agree with it.
        public string? CourseType { get; set; }

        // Audience: All (default), Course (courseId), Batch (batchId) , Users (userIds) or SyllabusPaid
        public string? AudienceType { get; set; }
        public string? CourseId { get; set; }
        public string? BatchId { get; set; }
        public List<string>? UserIds { get; set; }
        // Shortcut for one student (added to userIds). When userId / userIds are sent the notification goes to exactly those
        // students; courseType, audienceType, courseId and batchId are ignored
        public string? UserId { get; set; }

        // Flash Notifications: show it to students from PushStartDate until PushEndDate (date and time; send an ISO time with Z or an
        // offset). Both optional. With sendPush, a future PushStartDate is also when the push goes out.
        // Job Notifications ignore these and use StartDate / EndDate instead (and Flash ignores StartDate / EndDate).
        // Job Notifications also ignore courseType, audienceType, courseId, batchId and users - they always go to everyone.
        public DateTime? PushStartDate { get; set; }
        public DateTime? PushEndDate { get; set; }

        // true also sends a push to the audience's devices: within seconds of saving, or at PushStartDate if that is still in the future.
        public bool? SendPush { get; set; }
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
        // Flash Notification only: "Paid" or "Free" (see create). Ignored for Job Notifications.
        public string? CourseType { get; set; }
        public string? AudienceType { get; set; }
        public string? CourseId { get; set; }
        public string? BatchId { get; set; }
        public List<string>? UserIds { get; set; }
        public string? UserId { get; set; }
        public DateTime? PushStartDate { get; set; }
        public DateTime? PushEndDate { get; set; }
        public bool? SendPush { get; set; }
    }
}

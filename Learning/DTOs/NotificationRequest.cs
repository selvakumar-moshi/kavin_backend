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
    }
}

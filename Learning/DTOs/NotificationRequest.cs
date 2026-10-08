namespace LearningBackendAPI.DTOs
{
    public class NotificationCreateRequest
    {
        // "Push Notification" or "Job Notification"
        public string? NotificationType { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Link { get; set; }
        public DateTime? Date { get; set; }
    }

    public class NotificationUpdateRequest
    {
        public string? NotificationType { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Link { get; set; }
        public DateTime? Date { get; set; }
    }
}

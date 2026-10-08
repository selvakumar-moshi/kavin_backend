namespace LearningBackendAPI.DTOs
{
    public class NotificationSearchRequest
    {
        // Optional: only "Push Notification" or only "Job Notification"
        public string? NotificationType { get; set; }
        public string? SearchTerm { get; set; }
        public Dictionary<string, string>? GlobalFilter { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}

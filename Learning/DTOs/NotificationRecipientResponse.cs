namespace LearningBackendAPI.DTOs
{
    // One student in GET /api/Notification/recipients
    public class NotificationRecipientResponse
    {
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public string? ApplicationNo { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
    }
}

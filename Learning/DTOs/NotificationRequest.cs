namespace LearningBackendAPI.DTOs
{
    public class NotificationCreateRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Link { get; set; }
        public DateTime? Date { get; set; }
    }

    public class NotificationUpdateRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Link { get; set; }
        public DateTime? Date { get; set; }
    }
}

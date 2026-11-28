namespace LearningBackendAPI.DTOs
{
    public class BatchCreateRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? CourseId { get; set; }
        public DateTime? BatchFrom { get; set; }
        public DateTime? BatchTo { get; set; }
        public string? WhatsAppLink { get; set; }
        public string? TelegramLink { get; set; }
        // Optional PDF shown in the mobile app (max 10 MB)
        public IFormFile? File { get; set; }
    }

    public class BatchUpdateRequest
    {
        public string? Title { get; set; }
        // Leave out to keep it; send "" to clear it
        public string? Description { get; set; }
        public string? CourseId { get; set; }
        public DateTime? BatchFrom { get; set; }
        public DateTime? BatchTo { get; set; }
        public string? WhatsAppLink { get; set; }
        public string? TelegramLink { get; set; }
        // A new PDF replaces the current one
        public IFormFile? File { get; set; }
        // true removes the current PDF (ignored when a new File is sent)
        public bool? RemoveFile { get; set; }
    }
}

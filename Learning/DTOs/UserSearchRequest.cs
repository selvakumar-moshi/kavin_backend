namespace LearningBackendAPI.DTOs
{
    public class UserSearchRequest
    {
        public string? SearchTerm { get; set; }
        public Dictionary<string, string>? GlobalFilter { get; set; }
        // Last payment screenshot upload date, inclusive range (UTC). A date without a time as "to" covers that whole day.
        public DateTime? PaymentUploadedFrom { get; set; }
        public DateTime? PaymentUploadedTo { get; set; }
        // Join date range, same rules
        public DateTime? JoinedFrom { get; set; }
        public DateTime? JoinedTo { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}

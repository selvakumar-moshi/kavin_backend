namespace LearningBackendAPI.DTOs
{
    // POST / PUT /api/LiveClasses. On update only the fields that are sent change.
    // "status" is accepted but ignored: it is always worked out from date, startTime and endTime.
    public class LiveClassRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        // Optional http(s) links. On update, leave out to keep, send "" to clear.
        public string? YouTubeLink { get; set; }
        public string? GoogleMeetLink { get; set; }
        // "yyyy-MM-dd" (an ISO date-time such as "2026-11-20T00:00:00Z" is also accepted: only the date part is used)
        public string? Date { get; set; }
        // "HH:mm" (24 hour) or "h:mm AM/PM"
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? Status { get; set; }
    }
}

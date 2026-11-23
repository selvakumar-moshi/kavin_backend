namespace LearningBackendAPI.DTOs
{
    // POST /api/Quiz/{id}/publish. Every field is optional, send only what applies.
    // A date-time with a Z or an offset ("2026-11-25T09:00:00+05:30") is taken as written; one without
    // ("2026-11-25T09:00:00") is the institute's local time.
    public class QuizPublishRequest
    {
        // The quiz opens at this time (before it, students see it but can't open it). Leave out to open immediately.
        public DateTime? StartDate { get; set; }
        // The quiz expires at this time. Leave out for no end.
        public DateTime? EndDate { get; set; }
        // Publish now and expire at this time. Can't be combined with startDate / endDate.
        public DateTime? ExpireAt { get; set; }

        // Show each student the questions in a different order (default: true)
        public bool? ShuffleQuestions { get; set; }
    }
}

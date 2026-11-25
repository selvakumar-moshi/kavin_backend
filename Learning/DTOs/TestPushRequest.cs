namespace LearningBackendAPI.DTOs
{
    // POST /api/Device/test-push (admin)
    public class TestPushRequest
    {
        // The user whose registered devices receive the test push
        public string UserId { get; set; } = "";
        // Optional; defaults to "Test push"
        public string? Title { get; set; }
        // Optional; defaults to a short test message
        public string? Body { get; set; }
    }
}

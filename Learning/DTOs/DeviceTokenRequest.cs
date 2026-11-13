namespace LearningBackendAPI.DTOs
{
    public class DeviceTokenRequest
    {
        // The FCM registration token from the mobile app
        public string Token { get; set; } = "";
        // Optional: "android" or "ios"
        public string? Platform { get; set; }
    }
}

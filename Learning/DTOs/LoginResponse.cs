using System.Text.Json.Serialization;

namespace LearningBackendAPI.DTOs
{
    public class LoginResponse
    {
        public string Token { get; set; }
        public UserDto User { get; set; }
    }

    public class UserDto
    {
        [JsonPropertyName("userId")]
        public string Id { get; set; }
        public string? ApplicationNo { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Role { get; set; }
        public string ProfileImage { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? EnrollmentId { get; set; }
        public string? EnrollmentStatus { get; set; }
    }
}
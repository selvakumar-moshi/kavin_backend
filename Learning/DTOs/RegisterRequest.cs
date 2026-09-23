namespace LearningBackendAPI.DTOs
{
    public class RegisterRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string District { get; set; }
        public string CourseId { get; set; }
        public string BatchId { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
    }
}
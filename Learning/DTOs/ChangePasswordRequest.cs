namespace LearningBackendAPI.DTOs
{
    public class ChangePasswordRequest
    {
        // The temporary password the admin issued (or, later, the user's current password)
        public string CurrentPassword { get; set; } = "";
        public string NewPassword { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";
    }
}

using LearningBackendAPI.DTOs;

namespace LearningBackendAPI.Services
{
    public interface IUserService
    {
        Task<PagedResult<UserDto>> GetAllUsersAsync(UserSearchRequest? request);
        Task<UserDto> GetUserByIdAsync(string id);
        Task<UserProfileResponse> GetUserProfileAsync(string id, bool includeAllowedBatches = false);
        Task<UserDto> UpdateUserAsync(string id, UpdateUserRequest request, bool allowCourseEnrollment = false, string? adminUserId = null);
        /// <summary>Sets a new random temporary password and returns it (the stored password is a hash and can't be shown).</summary>
        Task<string> ResetPasswordAsync(string id);
        Task<UserDto> UpdateProfileImageAsync(string id, IFormFile file);
        Task<bool> DeleteUserAsync(string id);
    }
}

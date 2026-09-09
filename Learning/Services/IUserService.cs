using LearningBackendAPI.DTOs;

namespace LearningBackendAPI.Services
{
    public interface IUserService
    {
        Task<PagedResult<UserDto>> GetAllUsersAsync(UserSearchRequest? request);
        Task<UserDto> GetUserByIdAsync(string id);
        Task<UserProfileResponse> GetUserProfileAsync(string id);
        Task<UserDto> UpdateUserAsync(string id, UpdateUserRequest request, bool allowCourseEnrollment = false);
        Task<UserDto> UpdateProfileImageAsync(string id, IFormFile file);
        Task<bool> DeleteUserAsync(string id);
    }
}

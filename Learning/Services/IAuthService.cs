using LearningBackendAPI.DTOs;

namespace LearningBackendAPI.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task<UserDto> RegisterAsync(RegisterRequest request);
        Task ChangePasswordAsync(string userId, ChangePasswordRequest request);
    }
}
using LearningBackendAPI.Models;

namespace LearningBackendAPI.Services
{
    public interface IJwtService
    {
        string GenerateToken(User user);
    }
}
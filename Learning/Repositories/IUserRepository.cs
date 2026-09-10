using LearningBackendAPI.Models;

namespace LearningBackendAPI.Repositories
{
    public interface IUserRepository
    {
        Task<User> GetByIdAsync(string id);
        Task<User> GetByEmailAsync(string email);
        Task<User> CreateAsync(User user);
        Task UpdateAsync(string id, User user);
        Task<bool> DeleteAsync(string id);
        Task<bool> EmailExistsAsync(string email);
        Task<(List<User> Users, long TotalCount)> GetAllAsync(int pageNumber, int pageSize);
        Task<(List<User> Users, long TotalCount)> SearchAsync(string? searchTerm, Dictionary<string, string>? fieldFilters, int pageNumber, int pageSize);
        Task<long> CountByRoleAsync(string role);
        Task<List<User>> GetUsersWithoutApplicationNoAsync();
    }
}
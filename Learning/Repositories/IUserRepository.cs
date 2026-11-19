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
        // paymentFrom/paymentTo: inclusive range on the last payment upload date; joinedFrom/joinedTo: inclusive range on the join date.
        // A "to" without a time of day covers that whole day.
        Task<(List<User> Users, long TotalCount)> SearchAsync(string? searchTerm, Dictionary<string, string>? fieldFilters, int pageNumber, int pageSize,
            DateTime? paymentFrom = null, DateTime? paymentTo = null, DateTime? joinedFrom = null, DateTime? joinedTo = null);
        Task<List<User>> GetAllByRoleAsync(string role);
        Task SetPaymentUploadedAtAsync(string id, DateTime when);
        Task<long> CountByRoleAsync(string role);
        Task<List<User>> GetUsersWithoutApplicationNoAsync();
        // Atomically registers the device only if none is saved yet; false = another device got there first
        Task<bool> TryRegisterDeviceAsync(string id, string deviceHash);
        Task SetLastLoginAsync(string id, DateTime when);
        // Clears the saved device; false = no such user
        Task<bool> ClearDeviceAsync(string id);
    }
}
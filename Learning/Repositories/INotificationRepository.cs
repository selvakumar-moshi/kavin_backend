using LearningBackendAPI.Models;

namespace LearningBackendAPI.Repositories
{
    public interface INotificationRepository
    {
        Task<Notification> GetByIdAsync(string id);
        Task<List<Notification>> GetAllAsync();
        Task<Notification> CreateAsync(Notification notification);
        Task UpdateAsync(string id, Notification notification);
        Task<bool> DeleteAsync(string id);
    }
}

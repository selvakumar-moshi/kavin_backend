using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;

namespace LearningBackendAPI.Services
{
    public interface INotificationService
    {
        Task<Notification> CreateNotificationAsync(NotificationCreateRequest request);
        Task<Notification> UpdateNotificationAsync(string id, NotificationUpdateRequest request);
        Task<bool> DeleteNotificationAsync(string id);
        Task<PagedResult<Notification>> GetAllNotificationsAsync(string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize);
    }
}

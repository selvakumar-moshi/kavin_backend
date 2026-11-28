using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;

namespace LearningBackendAPI.Services
{
    public interface INotificationService
    {
        Task<Notification> CreateNotificationAsync(NotificationCreateRequest request);
        Task<Notification> UpdateNotificationAsync(string id, NotificationUpdateRequest request);
        Task<bool> DeleteNotificationAsync(string id);
        /// <summary>Admins get every notification; a student only those whose audience covers them (without the push details).</summary>
        Task<PagedResult<Notification>> GetAllNotificationsAsync(string? notificationType, string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize, string? viewerUserId = null, bool isAdmin = true);
        /// <summary>Queues the push for now (first send or a resend). The scheduler sends it within seconds.</summary>
        Task<List<NotificationRecipientResponse>> GetBatchRecipientsAsync(string batchId);
        Task<Notification> QueuePushAsync(string id);
        Task<Notification> CancelPushAsync(string id);
    }
}

using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepository;

        public NotificationService(INotificationRepository notificationRepository)
        {
            _notificationRepository = notificationRepository;
        }

        public async Task<Notification> CreateNotificationAsync(NotificationCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                throw new InvalidOperationException("Title is required");
            }

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                throw new InvalidOperationException("Description is required");
            }

            if (request.Date == null)
            {
                throw new InvalidOperationException("Date is required");
            }

            var notification = new Notification
            {
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                Link = string.IsNullOrWhiteSpace(request.Link) ? null : request.Link.Trim(),
                Date = request.Date.Value,
                CreatedAt = DateTime.UtcNow
            };

            return await _notificationRepository.CreateAsync(notification);
        }

        public async Task<Notification> UpdateNotificationAsync(string id, NotificationUpdateRequest request)
        {
            var notification = await _notificationRepository.GetByIdAsync(id);
            if (notification == null)
            {
                throw new KeyNotFoundException(Constants.Messages.NotificationNotFound);
            }

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                notification.Title = request.Title.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Description))
            {
                notification.Description = request.Description.Trim();
            }

            if (request.Link != null)
            {
                notification.Link = string.IsNullOrWhiteSpace(request.Link) ? null : request.Link.Trim();
            }

            if (request.Date.HasValue)
            {
                notification.Date = request.Date.Value;
            }

            await _notificationRepository.UpdateAsync(id, notification);
            return notification;
        }

        public async Task<bool> DeleteNotificationAsync(string id)
        {
            var notification = await _notificationRepository.GetByIdAsync(id);
            if (notification == null)
            {
                throw new KeyNotFoundException(Constants.Messages.NotificationNotFound);
            }

            return await _notificationRepository.DeleteAsync(id);
        }

        private static readonly Dictionary<string, Func<Notification, string?>> SearchFields = new()
        {
            ["title"] = n => n.Title,
            ["description"] = n => n.Description
        };
        private static readonly string[] DefaultSearchFields = { "title", "description" };

        public async Task<PagedResult<Notification>> GetAllNotificationsAsync(string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize)
        {
            var notifications = await _notificationRepository.GetAllAsync();
            var filtered = TextSearchHelper.ApplyFilter(notifications, searchTerm, globalFilter, SearchFields, DefaultSearchFields);
            return PagingHelper.ToPagedResult(filtered, pageNumber, pageSize);
        }
    }
}

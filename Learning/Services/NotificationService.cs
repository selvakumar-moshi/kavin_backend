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

            var type = Constants.NotificationTypes.Normalize(request.NotificationType);
            if (type == null)
            {
                throw new InvalidOperationException("Notification type must be \"Push Notification\" or \"Job Notification\"");
            }

            // Date is required for Push notifications and optional for Job notifications
            if (type == Constants.NotificationTypes.Push && request.Date == null)
            {
                throw new InvalidOperationException("Date is required for a Push Notification");
            }

            var notification = new Notification
            {
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                Link = string.IsNullOrWhiteSpace(request.Link) ? null : request.Link.Trim(),
                NotificationType = type,
                Date = request.Date,
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

            if (!string.IsNullOrWhiteSpace(request.NotificationType))
            {
                var type = Constants.NotificationTypes.Normalize(request.NotificationType);
                if (type == null)
                {
                    throw new InvalidOperationException("Notification type must be \"Push Notification\" or \"Job Notification\"");
                }
                notification.NotificationType = type;
            }

            if (request.Date.HasValue)
            {
                notification.Date = request.Date.Value;
            }

            if (notification.NotificationType == Constants.NotificationTypes.Push && notification.Date == null)
            {
                throw new InvalidOperationException("Date is required for a Push Notification");
            }

            notification.UpdatedAt = DateTime.UtcNow;
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

        public async Task<PagedResult<Notification>> GetAllNotificationsAsync(string? notificationType, string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize)
        {
            var notifications = await _notificationRepository.GetAllAsync();

            if (!string.IsNullOrWhiteSpace(notificationType))
            {
                var type = Constants.NotificationTypes.Normalize(notificationType)
                    ?? throw new InvalidOperationException("Notification type must be \"Push Notification\" or \"Job Notification\"");
                notifications = notifications.Where(n => n.NotificationType == type).ToList();
            }

            // Newest first: by its date, or by when it was created if it has none
            notifications = notifications.OrderByDescending(n => n.Date ?? n.CreatedAt).ToList();

            var filtered = TextSearchHelper.ApplyFilter(notifications, searchTerm, globalFilter, SearchFields, DefaultSearchFields);
            return PagingHelper.ToPagedResult(filtered, pageNumber, pageSize);
        }
    }
}

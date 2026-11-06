using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public class NotificationService : INotificationService
    {
        private const string AttachmentFolder = "coaching/Notification";
        private const long MaxAttachmentSizeInBytes = 10 * 1024 * 1024; // 10 MB
        private static readonly string[] AttachmentExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".webp" };

        private readonly INotificationRepository _notificationRepository;
        private readonly IFileStorageService _fileStorageService;

        public NotificationService(INotificationRepository notificationRepository, IFileStorageService fileStorageService)
        {
            _notificationRepository = notificationRepository;
            _fileStorageService = fileStorageService;
        }

        public async Task<Notification> CreateNotificationAsync(NotificationCreateRequest request)
        {
            // Every field is optional. A type that isn't passed defaults to Flash; one that is passed must be valid.
            var type = Constants.NotificationTypes.Flash;
            if (!string.IsNullOrWhiteSpace(request.NotificationType))
            {
                type = Constants.NotificationTypes.Normalize(request.NotificationType)
                    ?? throw new InvalidOperationException("Notification type must be \"Flash Notification\" or \"Job Notification\"");
            }

            EnsureValidDateRange(request.StartDate, request.EndDate);
            ValidateAttachment(request.File);

            var notification = new Notification
            {
                Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Link = string.IsNullOrWhiteSpace(request.Link) ? null : request.Link.Trim(),
                NotificationType = type,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                CreatedAt = DateTime.UtcNow
            };

            if (HasFile(request.File))
            {
                await AttachAsync(notification, request.File!);
            }

            try
            {
                return await _notificationRepository.CreateAsync(notification);
            }
            catch
            {
                // Don't leave an orphaned file behind if the notification wasn't saved
                await _fileStorageService.DeleteAsync(notification.AttachmentKey ?? "");
                throw;
            }
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
                    throw new InvalidOperationException("Notification type must be \"Flash Notification\" or \"Job Notification\"");
                }
                notification.NotificationType = type;
            }

            if (request.StartDate.HasValue)
            {
                notification.StartDate = request.StartDate.Value;
            }

            if (request.EndDate.HasValue)
            {
                notification.EndDate = request.EndDate.Value;
            }

            EnsureValidDateRange(notification.StartDate, notification.EndDate);
            ValidateAttachment(request.File);

            // The old file is only removed once the notification is saved with its replacement
            string? oldKey = null;
            if (HasFile(request.File))
            {
                oldKey = notification.AttachmentKey;
                await AttachAsync(notification, request.File!);
            }
            else if (request.RemoveFile == true && notification.AttachmentKey != null)
            {
                oldKey = notification.AttachmentKey;
                notification.AttachmentUrl = null;
                notification.AttachmentName = null;
                notification.AttachmentKey = null;
            }

            notification.UpdatedAt = DateTime.UtcNow;
            await _notificationRepository.UpdateAsync(id, notification);

            if (oldKey != null)
            {
                await _fileStorageService.DeleteAsync(oldKey);
            }
            return notification;
        }

        public async Task<bool> DeleteNotificationAsync(string id)
        {
            var notification = await _notificationRepository.GetByIdAsync(id);
            if (notification == null)
            {
                throw new KeyNotFoundException(Constants.Messages.NotificationNotFound);
            }

            var deleted = await _notificationRepository.DeleteAsync(id);
            if (deleted && notification.AttachmentKey != null)
            {
                await _fileStorageService.DeleteAsync(notification.AttachmentKey);
            }
            return deleted;
        }

        private static bool HasFile(IFormFile? file) => file != null && file.Length > 0;

        private static void ValidateAttachment(IFormFile? file)
        {
            if (!HasFile(file))
            {
                return;
            }

            var extension = Path.GetExtension(file!.FileName).ToLowerInvariant();
            if (!AttachmentExtensions.Contains(extension))
            {
                throw new InvalidOperationException("Attachment must be a PDF, or a JPG, PNG or WEBP image");
            }

            if (file.Length > MaxAttachmentSizeInBytes)
            {
                throw new InvalidOperationException("Attachment must be 10 MB or smaller");
            }
        }

        // Unique key per upload, so a file never overwrites (or is overwritten by) another notification's file
        private async Task AttachAsync(Notification notification, IFormFile file)
        {
            var originalName = Path.GetFileName(file.FileName);
            var uniqueName = $"{originalName}";
            (string Url, string Key) uploaded;
            try
            {
                uploaded = Path.GetExtension(originalName).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                    ? await _fileStorageService.UploadPdfAsync(file, AttachmentFolder, uniqueName)
                    : await _fileStorageService.UploadImageAsync(file, AttachmentFolder, uniqueName);
            }
            catch (Exception ex) when (ex is Amazon.Runtime.AmazonServiceException or Amazon.Runtime.AmazonClientException)
            {
                // Say what AWS said (e.g. AccessDenied, or no credentials / IAM role found) instead of a bare "unexpected error"
                var reason = (ex as Amazon.Runtime.AmazonServiceException)?.ErrorCode ?? "no AWS credentials or IAM role available";
                throw new InvalidOperationException($"Attachment upload to S3 failed: {reason}. Check the AWS credentials / IAM role and its S3 permissions (s3:PutObject) for the bucket.", ex);
            }
            var (url, key) = uploaded;

            notification.AttachmentUrl = url;
            notification.AttachmentName = originalName;
            notification.AttachmentKey = key;
        }

        private static readonly Dictionary<string, Func<Notification, string?>> SearchFields = new()
        {
            ["title"] = n => n.Title,
            ["description"] = n => n.Description
        };
        private static readonly string[] DefaultSearchFields = { "title", "description" };

        private static void EnsureValidDateRange(DateTime? startDate, DateTime? endDate)
        {
            if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value)
            {
                throw new InvalidOperationException("EndDate can't be before StartDate");
            }
        }

        public async Task<PagedResult<Notification>> GetAllNotificationsAsync(string? notificationType, string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize)
        {
            var notifications = await _notificationRepository.GetAllAsync();

            if (!string.IsNullOrWhiteSpace(notificationType))
            {
                var type = Constants.NotificationTypes.Normalize(notificationType)
                    ?? throw new InvalidOperationException("Notification type must be \"Flash Notification\" or \"Job Notification\"");
                notifications = notifications.Where(n => Constants.NotificationTypes.Normalize(n.NotificationType) == type).ToList();
            }

            // Newest first: by its start date, or by when it was created if it has none
            notifications = notifications.OrderByDescending(n => n.StartDate ?? n.CreatedAt).ToList();

            var filtered = TextSearchHelper.ApplyFilter(notifications, searchTerm, globalFilter, SearchFields, DefaultSearchFields);
            return PagingHelper.ToPagedResult(filtered, pageNumber, pageSize);
        }
    }
}

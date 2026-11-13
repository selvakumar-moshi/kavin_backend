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
        private readonly IEnrollmentRepository _enrollmentRepository;
        private readonly ISyllabusWiseRepository _syllabusWiseRepository;

        public NotificationService(
            INotificationRepository notificationRepository,
            IFileStorageService fileStorageService,
            IEnrollmentRepository enrollmentRepository,
            ISyllabusWiseRepository syllabusWiseRepository)
        {
            _notificationRepository = notificationRepository;
            _fileStorageService = fileStorageService;
            _enrollmentRepository = enrollmentRepository;
            _syllabusWiseRepository = syllabusWiseRepository;
        }

        // Validates the audience and fills the notification's audience fields. A value that isn't passed keeps its current one.
        private static void ApplyAudience(Notification notification, string? audienceType, string? courseId, string? batchId, List<string>? userIds)
        {
            var type = notification.AudienceType;
            if (!string.IsNullOrWhiteSpace(audienceType))
            {
                type = Constants.NotificationAudiences.Normalize(audienceType)
                    ?? throw new InvalidOperationException("audienceType must be All, Course, Batch, Users or SyllabusPaid");
            }

            var course = string.IsNullOrWhiteSpace(courseId) ? notification.CourseId : courseId.Trim();
            var batch = string.IsNullOrWhiteSpace(batchId) ? notification.BatchId : batchId.Trim();
            var users = userIds != null
                ? userIds.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).Distinct().ToList()
                : notification.UserIds;

            if (type == Constants.NotificationAudiences.Course && string.IsNullOrEmpty(course))
                throw new InvalidOperationException("courseId is required for a Course audience");
            if (type == Constants.NotificationAudiences.Batch && string.IsNullOrEmpty(batch))
                throw new InvalidOperationException("batchId is required for a Batch audience");
            if (type == Constants.NotificationAudiences.Users && (users == null || users.Count == 0))
                throw new InvalidOperationException("userIds is required for a Users audience");

            // Keep only what the chosen audience uses
            notification.AudienceType = type;
            notification.CourseId = type == Constants.NotificationAudiences.Course ? course : null;
            notification.BatchId = type == Constants.NotificationAudiences.Batch ? batch : null;
            notification.UserIds = type == Constants.NotificationAudiences.Users ? users : null;
        }

        // Treat a time without an offset as UTC, so the stored value doesn't depend on the server's time zone
        private static DateTime ToUtc(DateTime value)
            => value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

        private static void QueuePush(Notification notification, DateTime? scheduledAt)
        {
            if (string.IsNullOrWhiteSpace(notification.Title) && string.IsNullOrWhiteSpace(notification.Description))
            {
                throw new InvalidOperationException("A push needs a title or a description");
            }

            var now = DateTime.UtcNow;
            var at = scheduledAt.HasValue ? ToUtc(scheduledAt.Value) : now;
            notification.SendPush = true;
            notification.PushStatus = Constants.PushStatuses.Scheduled;
            notification.PushScheduledAt = at < now ? now : at;
            notification.PushError = null;
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

            ApplyAudience(notification, request.AudienceType, request.CourseId, request.BatchId, request.UserIds);
            if (request.SendPush == true)
            {
                QueuePush(notification, request.PushScheduledAt);
            }

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

            if (notification.PushStatus == Constants.PushStatuses.Sending)
            {
                throw new InvalidOperationException("This notification's push is being sent right now - try again in a moment");
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

            ApplyAudience(notification, request.AudienceType, request.CourseId, request.BatchId, request.UserIds);

            // Push: only one that hasn't gone out can be changed here; a sent one is resent via POST {id}/push
            var pushPending = notification.PushStatus is Constants.PushStatuses.None or Constants.PushStatuses.Cancelled or Constants.PushStatuses.Scheduled;
            if (request.SendPush == true)
            {
                if (!pushPending)
                {
                    throw new InvalidOperationException("This push has already been sent - use POST /api/Notification/{id}/push to send it again");
                }
                QueuePush(notification, request.PushScheduledAt ?? notification.PushScheduledAt);
            }
            else if (request.SendPush == false && notification.PushStatus == Constants.PushStatuses.Scheduled)
            {
                notification.SendPush = false;
                notification.PushStatus = Constants.PushStatuses.Cancelled;
            }
            else if (request.PushScheduledAt.HasValue && notification.PushStatus == Constants.PushStatuses.Scheduled)
            {
                QueuePush(notification, request.PushScheduledAt);
            }

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

        public async Task<Notification> QueuePushAsync(string id)
        {
            var notification = await _notificationRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException(Constants.Messages.NotificationNotFound);
            if (string.IsNullOrWhiteSpace(notification.Title) && string.IsNullOrWhiteSpace(notification.Description))
            {
                throw new InvalidOperationException("A push needs a title or a description");
            }
            if (!await _notificationRepository.QueuePushNowAsync(id))
            {
                throw new InvalidOperationException("This notification's push is being sent right now");
            }
            return await _notificationRepository.GetByIdAsync(id);
        }

        public async Task<Notification> CancelPushAsync(string id)
        {
            var notification = await _notificationRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException(Constants.Messages.NotificationNotFound);
            if (!await _notificationRepository.CancelScheduledPushAsync(id))
            {
                throw new InvalidOperationException($"Only a scheduled push can be cancelled (this one is {notification.PushStatus})");
            }
            return await _notificationRepository.GetByIdAsync(id);
        }

        // A student sees a notification when its audience covers them
        private async Task<List<Notification>> VisibleToAsync(List<Notification> notifications, string? userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return notifications.Where(n => n.AudienceType == Constants.NotificationAudiences.All).ToList();
            }

            var verified = (await _enrollmentRepository.GetByUserIdAsync(userId))
                .Where(e => e.Status == Constants.EnrollmentStatuses.Verified).ToList();
            var courseIds = verified.Select(e => e.CourseId).ToHashSet();
            var batchIds = verified.Where(e => e.BatchId != null).Select(e => e.BatchId!).ToHashSet();
            var hasSyllabusPayment = (await _syllabusWiseRepository.GetVerifiedPaymentsAsync(userId)).Count > 0;

            return notifications.Where(n => n.AudienceType switch
            {
                Constants.NotificationAudiences.Course => n.CourseId != null && courseIds.Contains(n.CourseId),
                Constants.NotificationAudiences.Batch => n.BatchId != null && batchIds.Contains(n.BatchId),
                Constants.NotificationAudiences.Users => n.UserIds != null && n.UserIds.Contains(userId),
                Constants.NotificationAudiences.SyllabusPaid => hasSyllabusPayment,
                _ => true
            }).ToList();
        }

        // Students get the content, not the admin-side targeting and delivery details
        private static void HidePushDetails(Notification n)
        {
            n.UserIds = null;
            n.SendPush = false;
            n.PushStatus = Constants.PushStatuses.None;
            n.PushScheduledAt = null;
            n.PushSentAt = null;
            n.PushTargetCount = 0;
            n.PushSuccessCount = 0;
            n.PushFailureCount = 0;
            n.PushError = null;
        }

        public async Task<PagedResult<Notification>> GetAllNotificationsAsync(string? notificationType, string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize, string? viewerUserId = null, bool isAdmin = true)
        {
            var notifications = await _notificationRepository.GetAllAsync();

            if (!isAdmin)
            {
                notifications = await VisibleToAsync(notifications, viewerUserId);
                notifications.ForEach(HidePushDetails);
            }

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

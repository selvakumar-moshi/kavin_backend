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
        private readonly ICourseRepository _courseRepository;
        private readonly IBatchRepository _batchRepository;
        private readonly IUserRepository _userRepository;

        public NotificationService(
            INotificationRepository notificationRepository,
            IFileStorageService fileStorageService,
            IEnrollmentRepository enrollmentRepository,
            ISyllabusWiseRepository syllabusWiseRepository,
            ICourseRepository courseRepository,
            IBatchRepository batchRepository,
            IUserRepository userRepository)
        {
            _userRepository = userRepository;
            _courseRepository = courseRepository;
            _batchRepository = batchRepository;
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
            // A Batch audience keeps the course too (Paid Flash notifications show which course the batch is in); only the batch decides who sees it
            notification.CourseId = type is Constants.NotificationAudiences.Course or Constants.NotificationAudiences.Batch ? course : null;
            notification.BatchId = type == Constants.NotificationAudiences.Batch ? batch : null;
            notification.UserIds = type == Constants.NotificationAudiences.Users ? users : null;
        }

        // Flash Notifications: turns courseType into the audience. Paid = students who bought the course (or the batch, if one is
        // given) -> Course / Batch audience; Free = everyone -> All. Returns the audience fields for ApplyAudience, so visibility and
        // the push recipients follow the existing audience logic. An explicit audienceType that contradicts courseType is rejected.
        private async Task<(string CourseType, string AudienceType, string? CourseId, string? BatchId)> ResolveCourseTypeAsync(
            string courseType, string? audienceType, string? courseId, string? batchId)
        {
            var normalized = Constants.MaterialAccess.All.FirstOrDefault(a => string.Equals(a, courseType.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("courseType must be Paid or Free");
            var audience = string.IsNullOrWhiteSpace(audienceType)
                ? null
                : Constants.NotificationAudiences.Normalize(audienceType)
                    ?? throw new InvalidOperationException("audienceType must be All, Course, Batch, Users or SyllabusPaid");

            if (normalized == Constants.MaterialAccess.Free)
            {
                if (audience != null && audience != Constants.NotificationAudiences.All)
                {
                    throw new InvalidOperationException("A Free notification goes to everyone - leave audienceType out or set it to All");
                }
                return (normalized, Constants.NotificationAudiences.All, null, null);
            }

            if (audience != null && audience != Constants.NotificationAudiences.Course && audience != Constants.NotificationAudiences.Batch)
            {
                throw new InvalidOperationException("A Paid notification goes to the students of a course or batch - leave audienceType out or set it to Course or Batch");
            }
            if (string.IsNullOrWhiteSpace(courseId))
            {
                throw new InvalidOperationException("courseId is required for a Paid notification");
            }

            var course = await _courseRepository.GetByIdAsync(courseId.Trim())
                ?? throw new InvalidOperationException(Constants.Messages.CourseNotFound);

            if (string.IsNullOrWhiteSpace(batchId))
            {
                return (normalized, Constants.NotificationAudiences.Course, course.Id, null);
            }

            var batch = await _batchRepository.GetByIdAsync(batchId.Trim())
                ?? throw new InvalidOperationException(Constants.Messages.BatchNotFound);
            if (batch.CourseId != course.Id)
            {
                throw new InvalidOperationException(Constants.Messages.BatchCourseMismatch);
            }
            return (normalized, Constants.NotificationAudiences.Batch, course.Id, batch.Id);
        }

        // userIds plus the single userId; null when neither was sent (so an update keeps the saved users)
        private static List<string>? MergeUsers(List<string>? userIds, string? userId)
        {
            if (userIds == null && string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }
            return (userIds ?? new List<string>()).Concat(new[] { userId ?? "" })
                .Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).Distinct().ToList();
        }

        private static DateTime? ToUtc(DateTime? value) => value.HasValue ? ToUtc(value.Value) : null;

        private static void EnsureValidPushWindow(DateTime? start, DateTime? end)
        {
            if (start.HasValue && end.HasValue && end.Value <= start.Value)
            {
                throw new InvalidOperationException("PushEndDate must be after PushStartDate");
            }
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

            // Job Notifications use startDate / endDate; Flash Notifications use pushStartDate / pushEndDate. The other pair is ignored.
            var isJob = type == Constants.NotificationTypes.Job;
            EnsureValidDateRange(isJob ? request.StartDate : null, isJob ? request.EndDate : null);
            ValidateAttachment(request.File);

            var notification = new Notification
            {
                Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Link = string.IsNullOrWhiteSpace(request.Link) ? null : request.Link.Trim(),
                NotificationType = type,
                StartDate = isJob ? request.StartDate : null,
                EndDate = isJob ? request.EndDate : null,
                PushStartDate = isJob ? null : ToUtc(request.PushStartDate),
                PushEndDate = isJob ? null : ToUtc(request.PushEndDate),
                CreatedAt = DateTime.UtcNow
            };
            EnsureValidPushWindow(notification.PushStartDate, notification.PushEndDate);

            if (type == Constants.NotificationTypes.Job)
            {
                // A Job Notification always goes to everyone: courseType, audienceType, courseId, batchId and users are ignored
                ApplyAudience(notification, Constants.NotificationAudiences.All, null, null, null);
            }
            else
            {
                var audienceType = request.AudienceType;
                var courseId = request.CourseId;
                var batchId = request.BatchId;
                var users = MergeUsers(request.UserIds, request.UserId);
                if (users is { Count: > 0 })
                {
                    // userIds always win: it goes to exactly those users, whatever course / batch / audience was also sent
                    (audienceType, courseId, batchId) = (Constants.NotificationAudiences.Users, null, null);
                }
                else if (!string.IsNullOrWhiteSpace(request.CourseType))
                {
                    var resolved = await ResolveCourseTypeAsync(request.CourseType, request.AudienceType, request.CourseId, request.BatchId);
                    notification.CourseType = resolved.CourseType;
                    (audienceType, courseId, batchId) = (resolved.AudienceType, resolved.CourseId, resolved.BatchId);
                }

                ApplyAudience(notification, audienceType, courseId, batchId, users);
            }

            if (request.SendPush == true)
            {
                QueuePush(notification, notification.PushStartDate);
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

            var typeChanged = false;
            if (!string.IsNullOrWhiteSpace(request.NotificationType))
            {
                var type = Constants.NotificationTypes.Normalize(request.NotificationType);
                if (type == null)
                {
                    throw new InvalidOperationException("Notification type must be \"Flash Notification\" or \"Job Notification\"");
                }
                typeChanged = Constants.NotificationTypes.Normalize(notification.NotificationType) != type;
                notification.NotificationType = type;
            }

            // Job Notifications use startDate / endDate; Flash Notifications use pushStartDate / pushEndDate. The other pair is ignored
            // (and cleared when the notification changes type).
            if (notification.NotificationType == Constants.NotificationTypes.Job)
            {
                notification.PushStartDate = null;
                notification.PushEndDate = null;
                if (request.StartDate.HasValue)
                {
                    notification.StartDate = request.StartDate.Value;
                }
                if (request.EndDate.HasValue)
                {
                    notification.EndDate = request.EndDate.Value;
                }
            }
            else
            {
                if (typeChanged)
                {
                    notification.StartDate = null;
                    notification.EndDate = null;
                }
                if (request.PushStartDate.HasValue)
                {
                    notification.PushStartDate = ToUtc(request.PushStartDate);
                }
                if (request.PushEndDate.HasValue)
                {
                    notification.PushEndDate = ToUtc(request.PushEndDate);
                }
            }

            EnsureValidDateRange(notification.StartDate, notification.EndDate);
            EnsureValidPushWindow(notification.PushStartDate, notification.PushEndDate);
            ValidateAttachment(request.File);

            // A Job Notification always goes to everyone; courseType / audience / users only apply to Flash Notifications.
            var audienceType = request.AudienceType;
            var courseId = request.CourseId;
            var batchId = request.BatchId;
            var users = MergeUsers(request.UserIds, request.UserId);
            if (notification.NotificationType != Constants.NotificationTypes.Flash)
            {
                notification.CourseType = null;
                (audienceType, courseId, batchId, users) = (Constants.NotificationAudiences.All, null, null, null);
            }
            else
            {
                var changingPaid = notification.CourseType == Constants.MaterialAccess.Paid &&
                    (!string.IsNullOrWhiteSpace(request.CourseId) || !string.IsNullOrWhiteSpace(request.BatchId) || !string.IsNullOrWhiteSpace(request.AudienceType));
                if (users is { Count: > 0 })
                {
                    // userIds always win: it goes to exactly those users, whatever course / batch / audience was also sent
                    notification.CourseType = null;
                    (audienceType, courseId, batchId) = (Constants.NotificationAudiences.Users, null, null);
                }
                // Re-resolve when courseType is sent, or when a Paid notification's course / batch / audience is being changed
                else if (!string.IsNullOrWhiteSpace(request.CourseType) || changingPaid)
                {
                    var wanted = string.IsNullOrWhiteSpace(request.CourseType) ? notification.CourseType! : request.CourseType;
                    // Fall back to what is saved for the fields that weren't sent
                    var resolved = await ResolveCourseTypeAsync(
                        wanted,
                        request.AudienceType,
                        string.IsNullOrWhiteSpace(request.CourseId) ? notification.CourseId : request.CourseId,
                        string.IsNullOrWhiteSpace(request.BatchId) ? notification.BatchId : request.BatchId);
                    notification.CourseType = resolved.CourseType;
                    (audienceType, courseId, batchId) = (resolved.AudienceType, resolved.CourseId, resolved.BatchId);
                }
                else if (notification.CourseType == null && string.IsNullOrWhiteSpace(audienceType) && string.IsNullOrWhiteSpace(courseId) && string.IsNullOrWhiteSpace(batchId) && users is { Count: > 0 })
                {
                    // Only users were given: it is for exactly them
                    audienceType = Constants.NotificationAudiences.Users;
                }
            }

            ApplyAudience(notification, audienceType, courseId, batchId, users);

            // Push: only one that hasn't gone out can be changed here; a sent one is resent via POST {id}/push
            var pushPending = notification.PushStatus is Constants.PushStatuses.None or Constants.PushStatuses.Cancelled or Constants.PushStatuses.Scheduled;
            if (request.SendPush == true)
            {
                if (!pushPending)
                {
                    throw new InvalidOperationException("This push has already been sent - use POST /api/Notification/{id}/push to send it again");
                }
                QueuePush(notification, notification.PushStartDate);
            }
            else if (request.SendPush == false && notification.PushStatus == Constants.PushStatuses.Scheduled)
            {
                notification.SendPush = false;
                notification.PushStatus = Constants.PushStatuses.Cancelled;
            }
            else if (request.PushStartDate.HasValue && notification.PushStatus == Constants.PushStatuses.Scheduled)
            {
                // PushStartDate moved: a push still waiting goes out at the new time
                QueuePush(notification, notification.PushStartDate);
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

        public async Task<List<NotificationRecipientResponse>> GetBatchRecipientsAsync(string batchId)
        {
            if (string.IsNullOrWhiteSpace(batchId))
            {
                throw new InvalidOperationException("batchId is required");
            }
            if (await _batchRepository.GetByIdAsync(batchId.Trim()) == null)
            {
                throw new KeyNotFoundException(Constants.Messages.BatchNotFound);
            }

            // Verified students only - the same people a Batch audience (and its push) reaches
            var userIds = (await _enrollmentRepository.GetAllAsync())
                .Where(e => e.Status == Constants.EnrollmentStatuses.Verified && e.BatchId == batchId.Trim())
                .Select(e => e.UserId).Distinct().ToList();

            var recipients = new List<NotificationRecipientResponse>();
            foreach (var userId in userIds)
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null) continue;
                recipients.Add(new NotificationRecipientResponse
                {
                    UserId = user.Id,
                    Name = $"{user.FirstName} {user.LastName}".Trim(),
                    ApplicationNo = user.ApplicationNo,
                    PhoneNumber = user.PhoneNumber,
                    Email = user.Email
                });
            }
            return recipients.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
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
                // Inside the PushStartDate .. PushEndDate window only (no dates = always)
                var now = DateTime.UtcNow;
                notifications = notifications
                    .Where(n => (n.PushStartDate == null || n.PushStartDate <= now) && (n.PushEndDate == null || n.PushEndDate >= now))
                    .ToList();
                notifications.ForEach(HidePushDetails);
            }

            if (!string.IsNullOrWhiteSpace(notificationType))
            {
                var type = Constants.NotificationTypes.Normalize(notificationType)
                    ?? throw new InvalidOperationException("Notification type must be \"Flash Notification\" or \"Job Notification\"");
                notifications = notifications.Where(n => Constants.NotificationTypes.Normalize(n.NotificationType) == type).ToList();
            }

            // Newest first: by its start date (pushStartDate / startDate), or by when it was created if it has none
            notifications = notifications.OrderByDescending(n => n.PushStartDate ?? n.StartDate ?? n.CreatedAt).ToList();

            var filtered = TextSearchHelper.ApplyFilter(notifications, searchTerm, globalFilter, SearchFields, DefaultSearchFields);
            return PagingHelper.ToPagedResult(filtered, pageNumber, pageSize);
        }
    }
}

using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public interface INotificationPushService
    {
        /// <summary>Sends every push that is due, and fails any that a restart left stuck in Sending.</summary>
        Task DispatchDueAsync(CancellationToken cancellationToken);
    }

    public class NotificationPushService : INotificationPushService
    {
        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        private readonly INotificationRepository _notificationRepository;
        private readonly IDeviceTokenRepository _deviceTokenRepository;
        private readonly IEnrollmentRepository _enrollmentRepository;
        private readonly ISyllabusWiseRepository _syllabusWiseRepository;
        private readonly IPushSender _pushSender;
        private readonly ILogger<NotificationPushService> _logger;

        public NotificationPushService(
            INotificationRepository notificationRepository,
            IDeviceTokenRepository deviceTokenRepository,
            IEnrollmentRepository enrollmentRepository,
            ISyllabusWiseRepository syllabusWiseRepository,
            IPushSender pushSender,
            ILogger<NotificationPushService> logger)
        {
            _notificationRepository = notificationRepository;
            _deviceTokenRepository = deviceTokenRepository;
            _enrollmentRepository = enrollmentRepository;
            _syllabusWiseRepository = syllabusWiseRepository;
            _pushSender = pushSender;
            _logger = logger;
        }

        public async Task DispatchDueAsync(CancellationToken cancellationToken)
        {
            await _notificationRepository.FailStaleSendingAsync(DateTime.UtcNow.AddMinutes(-10));

            foreach (var id in await _notificationRepository.GetDuePushIdsAsync(DateTime.UtcNow))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Atomic claim: only one worker gets it, so it can't go out twice
                var notification = await _notificationRepository.ClaimForPushAsync(id, onlyIfDue: true);
                if (notification == null)
                {
                    continue;
                }

                try
                {
                    await SendAsync(notification, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Push for notification {Id} failed", id);
                    await _notificationRepository.SetPushResultAsync(id, Constants.PushStatuses.Failed, 0, 0, 0, ex.Message);
                }
            }
        }

        private async Task SendAsync(Notification notification, CancellationToken cancellationToken)
        {
            var tokens = (await _deviceTokenRepository.GetTokensAsync(await ResolveAudienceUserIdsAsync(notification)))
                .Distinct().ToList();
            if (tokens.Count == 0)
            {
                await _notificationRepository.SetPushResultAsync(notification.Id, Constants.PushStatuses.Failed, 0, 0, 0,
                    "No registered devices for this audience");
                return;
            }

            var data = new Dictionary<string, string>
            {
                ["notificationId"] = notification.Id,
                ["notificationType"] = notification.NotificationType
            };
            if (!string.IsNullOrWhiteSpace(notification.Link))
            {
                data["link"] = notification.Link;
            }
            if (!string.IsNullOrWhiteSpace(notification.AttachmentUrl))
            {
                data["attachmentUrl"] = notification.AttachmentUrl;
            }

            // Only an image attachment can be shown in the push itself; a PDF travels in the data payload above
            var imageUrl = !string.IsNullOrWhiteSpace(notification.AttachmentName)
                && ImageExtensions.Contains(Path.GetExtension(notification.AttachmentName).ToLowerInvariant())
                ? notification.AttachmentUrl
                : null;

            var result = await _pushSender.SendAsync(tokens, new PushMessage(notification.Title, notification.Description, imageUrl, data), cancellationToken);

            if (result.InvalidTokens.Count > 0)
            {
                await _deviceTokenRepository.RemoveTokensAsync(result.InvalidTokens);
            }

            var status = result.Failure == 0 ? Constants.PushStatuses.Sent
                : result.Success == 0 ? Constants.PushStatuses.Failed
                : Constants.PushStatuses.PartiallyFailed;
            await _notificationRepository.SetPushResultAsync(notification.Id, status, tokens.Count, result.Success, result.Failure, result.Error);
        }

        // null means every device (the All audience)
        private async Task<List<string>?> ResolveAudienceUserIdsAsync(Notification notification)
        {
            switch (notification.AudienceType)
            {
                case Constants.NotificationAudiences.Users:
                    return notification.UserIds ?? new List<string>();

                case Constants.NotificationAudiences.Course:
                    return (await _enrollmentRepository.GetByCourseIdAsync(notification.CourseId ?? ""))
                        .Where(e => e.Status == Constants.EnrollmentStatuses.Verified)
                        .Select(e => e.UserId).Distinct().ToList();

                case Constants.NotificationAudiences.Batch:
                    return (await _enrollmentRepository.GetAllAsync())
                        .Where(e => e.Status == Constants.EnrollmentStatuses.Verified && e.BatchId == notification.BatchId)
                        .Select(e => e.UserId).Distinct().ToList();

                case Constants.NotificationAudiences.SyllabusPaid:
                    return (await _syllabusWiseRepository.GetPaymentsAsync())
                        .Where(p => p.PaymentStatus == Constants.PaymentStatuses.Verified)
                        .Select(p => p.UserId).Distinct().ToList();

                default:
                    return null;
            }
        }
    }
}

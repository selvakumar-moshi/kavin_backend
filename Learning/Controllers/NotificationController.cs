using System.Security.Claims;
using LearningBackendAPI.DTOs;
using LearningBackendAPI.Helpers;
using LearningBackendAPI.Services;
using LearningBackendAPI.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearningBackendAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ResponseHelper _responseHelper;

        public NotificationController(
            INotificationService notificationService,
            ResponseHelper responseHelper)
        {
            _notificationService = notificationService;
            _responseHelper = responseHelper;
        }

        /// <summary>
        /// Create a new notification (Admin only). Every field is optional: notificationType ("Flash Notification" -
        /// the default - or "Job Notification"), title, description, link, startDate, endDate, pushStartDate / pushEndDate (when
        /// students can see it). A Job Notification always goes to everyone (courseType, courseId, batchId are ignored); a push goes
        /// out only when sendPush is true; userId / userIds on their own send it to just those students. Sent as multipart/form-data so an optional "file" (PDF, or JPG / PNG / WEBP
        /// image, up to 10 MB) can be attached; the response carries attachmentUrl and attachmentName.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = Constants.Roles.Admin)]
        [RequestSizeLimit(12 * 1024 * 1024)]
        public async Task<IActionResult> CreateNotification([FromForm] NotificationCreateRequest request)
        {
            try
            {
                var notification = await _notificationService.CreateNotificationAsync(request);
                return Ok(_responseHelper.Success(notification, "Notification created successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
        }

        /// <summary>
        /// Update a notification (Admin only). multipart/form-data like create: a new "file" replaces the
        /// attachment, removeFile=true deletes it.
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        [RequestSizeLimit(12 * 1024 * 1024)]
        public async Task<IActionResult> UpdateNotification(string id, [FromForm] NotificationUpdateRequest request)
        {
            try
            {
                var notification = await _notificationService.UpdateNotificationAsync(id, request);
                return Ok(_responseHelper.Success(notification, "Notification updated successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
        }

        /// <summary>
        /// Admin: the students of a batch, for picking who a notification goes to. Flow: pick a course (GET /api/Course), then its
        /// batches (POST /api/Batch/search with { courseId }), then call this with the batch's id. Returns verified students only
        /// - userId, name, applicationNo, phoneNumber, email. Send the chosen userId values as userIds when creating the notification.
        /// </summary>
        [HttpGet("recipients")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> GetRecipients([FromQuery] string batchId)
        {
            try
            {
                var recipients = await _notificationService.GetBatchRecipientsAsync(batchId);
                return Ok(_responseHelper.Success(recipients, "Recipients retrieved successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
        }

        /// <summary>
        /// Delete a notification (Admin only)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> DeleteNotification(string id)
        {
            try
            {
                var result = await _notificationService.DeleteNotificationAsync(id);
                if (result)
                {
                    return Ok(_responseHelper.Success<object>(null, "Notification deleted successfully"));
                }
                return BadRequest(_responseHelper.BadRequest<object>("Failed to delete notification"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
        }

        /// <summary>
        /// Send this notification's push now (Admin only) - the first time, or again after it was sent. It is queued and
        /// goes out within seconds; the notification's pushStatus / pushSuccessCount / pushFailureCount show the outcome.
        /// </summary>
        [HttpPost("{id}/push")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> SendPush(string id)
        {
            try
            {
                var notification = await _notificationService.QueuePushAsync(id);
                return Ok(_responseHelper.Success(notification, "Push queued"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
        }

        /// <summary>
        /// Cancel a scheduled push that hasn't gone out yet (Admin only)
        /// </summary>
        [HttpPost("{id}/push/cancel")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> CancelPush(string id)
        {
            try
            {
                var notification = await _notificationService.CancelPushAsync(id);
                return Ok(_responseHelper.Success(notification, "Push cancelled"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
        }

        /// <summary>
        /// Get all notifications, filtered via a { notificationType (optional), searchTerm, globalFilter, pageNumber, pageSize } body payload
        /// (searchTerm: matches title or description; globalFilter: subset of title/description, both if
        /// omitted; pageNumber/pageSize default to 1/10 if omitted)
        /// </summary>
        [HttpPost("search")]
        public async Task<IActionResult> GetAllNotifications([FromBody] NotificationSearchRequest? request)
        {
            try
            {
                var isAdmin = User.IsInRole(Constants.Roles.Admin);
                var notifications = await _notificationService.GetAllNotificationsAsync(
                    request?.NotificationType, request?.SearchTerm, request?.GlobalFilter, request?.PageNumber ?? 1, request?.PageSize ?? 10,
                    User.FindFirstValue(ClaimTypes.NameIdentifier), isAdmin);
                return Ok(_responseHelper.Success(notifications, "Notifications retrieved successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
        }
    }
}

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
        /// Create a new notification (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> CreateNotification([FromBody] NotificationCreateRequest request)
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
        /// Update a notification (Admin only)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> UpdateNotification(string id, [FromBody] NotificationUpdateRequest request)
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
        /// Get all notifications, filtered via a { pageNumber, pageSize } body payload
        /// (pageNumber/pageSize default to 1/10 if omitted)
        /// </summary>
        [HttpPost("search")]
        public async Task<IActionResult> GetAllNotifications([FromBody] NotificationSearchRequest? request)
        {
            var notifications = await _notificationService.GetAllNotificationsAsync(
                request?.PageNumber ?? 1, request?.PageSize ?? 10);
            return Ok(_responseHelper.Success(notifications, "Notifications retrieved successfully"));
        }
    }
}

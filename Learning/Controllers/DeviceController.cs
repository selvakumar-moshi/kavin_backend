using System.Security.Claims;
using LearningBackendAPI.DTOs;
using LearningBackendAPI.Helpers;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Services;
using LearningBackendAPI.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearningBackendAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DeviceController : ControllerBase
    {
        private readonly IDeviceTokenRepository _deviceTokenRepository;
        private readonly ResponseHelper _responseHelper;
        private readonly IPushSender _pushSender;

        public DeviceController(IDeviceTokenRepository deviceTokenRepository, ResponseHelper responseHelper, IPushSender pushSender)
        {
            _deviceTokenRepository = deviceTokenRepository;
            _responseHelper = responseHelper;
            _pushSender = pushSender;
        }

        /// <summary>
        /// Admin: send a test push straight to one user's registered devices, bypassing the notification queue.
        /// Nothing is saved or shown in the in-app notification list. The response says how many devices FCM
        /// accepted and, if any failed, FCM's error - so it shows right away whether push works for that user.
        /// Devices FCM reports as uninstalled/expired are removed, as with a normal push.
        /// </summary>
        [HttpPost("test-push")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> SendTestPush([FromBody] TestPushRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                return BadRequest(_responseHelper.BadRequest<object>("UserId is required"));
            }

            var tokens = (await _deviceTokenRepository.GetTokensAsync(new[] { request.UserId.Trim() })).Distinct().ToList();
            if (tokens.Count == 0)
            {
                return BadRequest(_responseHelper.BadRequest<object>("This user has no registered devices - the app has to call POST /api/Device/token first"));
            }

            var message = new PushMessage(
                string.IsNullOrWhiteSpace(request.Title) ? "Test push" : request.Title.Trim(),
                string.IsNullOrWhiteSpace(request.Body) ? "This is a test push from the admin panel." : request.Body.Trim(),
                null,
                new Dictionary<string, string> { ["notificationType"] = "TestPush" });

            PushSendResult result;
            try
            {
                result = await _pushSender.SendAsync(tokens, message, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                // e.g. Firebase credentials are not configured
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }

            if (result.InvalidTokens.Count > 0)
            {
                await _deviceTokenRepository.RemoveTokensAsync(result.InvalidTokens);
            }

            var data = new
            {
                devices = tokens.Count,
                success = result.Success,
                failure = result.Failure,
                removedInvalidTokens = result.InvalidTokens.Count,
                error = result.Error
            };
            var text = result.Failure == 0 ? "Test push sent"
                : result.Success == 0 ? "Test push failed for every device"
                : "Test push sent to some devices only";
            return Ok(_responseHelper.Success(data, text));
        }

        /// <summary>
        /// Register the logged-in user's FCM token so they can receive push notifications. The app calls this after login
        /// and whenever Firebase refreshes the token; calling it again with the same token is harmless.
        /// </summary>
        [HttpPost("token")]
        public async Task<IActionResult> RegisterToken([FromBody] DeviceTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest(_responseHelper.BadRequest<object>("Token is required"));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var platform = string.IsNullOrWhiteSpace(request.Platform) ? null : request.Platform.Trim().ToLowerInvariant();
            await _deviceTokenRepository.UpsertAsync(userId, request.Token.Trim(), platform);
            return Ok(_responseHelper.Success<object>(null, "Device registered successfully"));
        }

        /// <summary>
        /// Admin: the registered device tokens, most recently seen first - for checking what the mobile app registered.
        /// Pass userId to see one user's devices; leave it out for everyone's (limit defaults to 50, max 200).
        /// Returns the full token (admin only), so it can be compared with what the app logs.
        /// </summary>
        [HttpGet("token")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> GetTokens([FromQuery] string? userId, [FromQuery] int? limit)
        {
            var take = Math.Clamp(limit ?? 50, 1, 200);
            var (items, total) = await _deviceTokenRepository.GetDevicesAsync(userId, take);
            var data = new
            {
                total,
                returned = items.Count,
                devices = items.Select(t => new
                {
                    id = t.Id,
                    userId = t.UserId,
                    platform = t.Platform,
                    token = t.Token,
                    createdAt = t.CreatedAt,
                    lastSeenAt = t.LastSeenAt
                })
            };
            return Ok(_responseHelper.Success(data, "Devices retrieved successfully"));
        }

        /// <summary>
        /// Unregister this device's token - call it on logout so the next person using the phone doesn't get this user's pushes.
        /// </summary>
        [HttpDelete("token")]
        public async Task<IActionResult> RemoveToken([FromBody] DeviceTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest(_responseHelper.BadRequest<object>("Token is required"));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _deviceTokenRepository.RemoveAsync(userId, request.Token.Trim());
            return Ok(_responseHelper.Success<object>(null, "Device unregistered successfully"));
        }
    }
}

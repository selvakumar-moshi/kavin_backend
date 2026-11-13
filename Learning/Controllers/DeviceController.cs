using System.Security.Claims;
using LearningBackendAPI.DTOs;
using LearningBackendAPI.Helpers;
using LearningBackendAPI.Repositories;
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

        public DeviceController(IDeviceTokenRepository deviceTokenRepository, ResponseHelper responseHelper)
        {
            _deviceTokenRepository = deviceTokenRepository;
            _responseHelper = responseHelper;
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

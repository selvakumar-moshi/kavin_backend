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
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ResponseHelper _responseHelper;

        public UserController(
            IUserService userService,
            ResponseHelper responseHelper)
        {
            _userService = userService;
            _responseHelper = responseHelper;
        }

        /// <summary>
        /// Get the current logged-in user's own details, including study materials for verified courses
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMyDetails()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                var user = await _userService.GetUserProfileAsync(userId);
                return Ok(_responseHelper.Success(user, "User details retrieved successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
        }

        /// <summary>
        /// Get all registered users, optionally filtered via a { searchTerm, globalFilter, pageNumber, pageSize }
        /// body payload (globalFilter: subset of firstName/lastName/phoneNumber/email/applicationNo/district; all
        /// six if omitted; pageNumber/pageSize default to 1/10 if omitted) (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> GetAllUsers([FromBody] UserSearchRequest? request)
        {
            var users = await _userService.GetAllUsersAsync(request);
            return Ok(_responseHelper.Success(users, "Users retrieved successfully"));
        }

        /// <summary>
        /// Get a specific user's details by ID, including study materials for verified courses (Admin only)
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> GetUserById(string id)
        {
            try
            {
                var user = await _userService.GetUserProfileAsync(id);
                return Ok(_responseHelper.Success(user, "User details retrieved successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
        }

        /// <summary>
        /// Update the current logged-in user's own details
        /// </summary>
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyDetails([FromBody] UpdateUserRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                var user = await _userService.UpdateUserAsync(userId, request);
                return Ok(_responseHelper.Success(user, "User details updated successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
        }

        /// <summary>
        /// Upload/replace the current logged-in user's profile image
        /// </summary>
        [HttpPut("me/profile-image")]
        [RequestSizeLimit(5 * 1024 * 1024)]
        public async Task<IActionResult> UpdateMyProfileImage(IFormFile file)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                var user = await _userService.UpdateProfileImageAsync(userId, file);
                return Ok(_responseHelper.Success(user, "Profile image updated successfully"));
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
        /// Update a specific user's details by ID (Admin only). To enroll the user in one or
        /// more additional courses (e.g. they purchased new courses later), also pass a
        /// courses: [{ courseId, batchId }, ...] array in the payload; each new-course enrollment is
        /// created as Pending and must then be verified via PUT /api/Enrollment/{id}/status. If the
        /// student previously dropped that course (its enrollment status is Dropped), passing that
        /// same courseId with a new batchId instead updates that existing enrollment in place -
        /// reactivated as Verified with the new batch, no re-payment/re-verification needed since
        /// the course was already purchased.
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _userService.UpdateUserAsync(id, request, allowCourseEnrollment: true, adminUserId: adminUserId);
                return Ok(_responseHelper.Success(user, "User details updated successfully"));
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
        /// Delete a user (Admin only)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == id)
            {
                return BadRequest(_responseHelper.BadRequest<object>("You cannot delete your own account"));
            }

            try
            {
                var result = await _userService.DeleteUserAsync(id);
                if (result)
                {
                    return Ok(_responseHelper.Success<object>(null, "User deleted successfully"));
                }
                return BadRequest(_responseHelper.BadRequest<object>("Failed to delete user"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
        }
    }
}

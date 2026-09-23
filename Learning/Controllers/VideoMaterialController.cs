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
    public class VideoMaterialController : ControllerBase
    {
        private readonly IVideoMaterialService _videoMaterialService;
        private readonly ResponseHelper _responseHelper;

        public VideoMaterialController(
            IVideoMaterialService videoMaterialService,
            ResponseHelper responseHelper)
        {
            _videoMaterialService = videoMaterialService;
            _responseHelper = responseHelper;
        }

        /// <summary>
        /// Create a new video material (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> CreateVideoMaterial([FromBody] VideoMaterialRequest request)
        {
            try
            {
                var result = await _videoMaterialService.CreateVideoMaterialAsync(request);
                return Ok(_responseHelper.Success(result, "Video material created successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
        }

        /// <summary>
        /// Get video materials the current user has access to (Admin sees all, students see only
        /// verified/paid courses), optionally filtered via a { courseId, material, searchTerm, globalFilter,
        /// pageNumber, pageSize } body payload (courseId: filter to a single course, omit/"All" for every
        /// accessible course; material: informational tag the UI sends, e.g. "video", not used to filter since
        /// this endpoint only ever returns video materials; globalFilter: subset of title/description/batchTitle,
        /// all three if omitted; pageNumber/pageSize default to 1/10 if omitted)
        /// </summary>
        [HttpPost("search")]
        public async Task<IActionResult> GetAllVideoMaterials([FromBody] MaterialSearchRequest? request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var role = User.FindFirstValue(ClaimTypes.Role) ?? Constants.Roles.User;
            var videoMaterials = await _videoMaterialService.GetAccessibleVideoMaterialsAsync(
                userId, role, request?.CourseId, request?.SearchTerm, request?.GlobalFilter, request?.PageNumber ?? 1, request?.PageSize ?? 10);
            return Ok(_responseHelper.Success(videoMaterials, "Video materials retrieved successfully"));
        }

        /// <summary>
        /// Get video material by ID (only accessible if the current user's course payment is verified, or is Admin)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetVideoMaterialById(string id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                var role = User.FindFirstValue(ClaimTypes.Role) ?? Constants.Roles.User;
                var videoMaterial = await _videoMaterialService.GetVideoMaterialByIdAsync(id, userId, role);
                return Ok(_responseHelper.Success(videoMaterial, "Video material retrieved successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, _responseHelper.Forbidden<object>(ex.Message));
            }
        }

        /// <summary>
        /// Update video material (Admin only)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> UpdateVideoMaterial(string id, [FromBody] VideoMaterialRequest request)
        {
            try
            {
                var videoMaterial = await _videoMaterialService.UpdateVideoMaterialAsync(id, request);
                return Ok(_responseHelper.Success(videoMaterial, "Video material updated successfully"));
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
        /// Delete video material (Admin only)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> DeleteVideoMaterial(string id)
        {
            try
            {
                var result = await _videoMaterialService.DeleteVideoMaterialAsync(id);
                if (result)
                {
                    return Ok(_responseHelper.Success<object>(null, "Video material deleted successfully"));
                }
                return BadRequest(_responseHelper.BadRequest<object>("Failed to delete video material"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
        }
    }
}

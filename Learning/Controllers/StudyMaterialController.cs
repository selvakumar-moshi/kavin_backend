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
    public class StudyMaterialController : ControllerBase
    {
        private readonly IStudyMaterialService _studyMaterialService;
        private readonly ResponseHelper _responseHelper;

        public StudyMaterialController(
            IStudyMaterialService studyMaterialService,
            ResponseHelper responseHelper)
        {
            _studyMaterialService = studyMaterialService;
            _responseHelper = responseHelper;
        }

        /// <summary>
        /// Create a new study material with a PDF upload
        /// </summary>
        [HttpPost]
        [Authorize(Roles = Constants.Roles.Admin)]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> CreateStudyMaterial([FromForm] StudyMaterialRequest request)
        {
            try
            {
                var result = await _studyMaterialService.CreateStudyMaterialAsync(request);
                return Ok(_responseHelper.Success(result, "Study material created successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
        }

        /// <summary>
        /// Get study materials the current user has access to (Admin sees all, students see only
        /// verified/paid courses), optionally filtered via a { courseId, material, searchTerm, globalFilter,
        /// pageNumber, pageSize } body payload (courseId: filter to a single course, omit/"All" for every
        /// accessible course; material: informational tag the UI sends, e.g. "study", not used to filter since
        /// this endpoint only ever returns study materials; globalFilter: subset of title/description/batchTitle,
        /// all three if omitted; pageNumber/pageSize default to 1/10 if omitted)
        /// </summary>
        [HttpPost("search")]
        public async Task<IActionResult> GetAllStudyMaterials([FromBody] MaterialSearchRequest? request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var role = User.FindFirstValue(ClaimTypes.Role) ?? Constants.Roles.User;
            var studyMaterials = await _studyMaterialService.GetAccessibleStudyMaterialsAsync(
                userId, role, request?.CourseId, request?.SearchTerm, request?.GlobalFilter, request?.PageNumber ?? 1, request?.PageSize ?? 10);
            return Ok(_responseHelper.Success(studyMaterials, "Study materials retrieved successfully"));
        }

        /// <summary>
        /// Get study material by ID (only accessible if the current user's course payment is verified, or is Admin)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetStudyMaterialById(string id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                var role = User.FindFirstValue(ClaimTypes.Role) ?? Constants.Roles.User;
                var studyMaterial = await _studyMaterialService.GetStudyMaterialByIdAsync(id, userId, role);
                return Ok(_responseHelper.Success(studyMaterial, "Study material retrieved successfully"));
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
        /// Update study material (optionally replacing the PDF)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> UpdateStudyMaterial(string id, [FromForm] StudyMaterialRequest request)
        {
            try
            {
                var studyMaterial = await _studyMaterialService.UpdateStudyMaterialAsync(id, request);
                return Ok(_responseHelper.Success(studyMaterial, "Study material updated successfully"));
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
        /// Delete study material
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> DeleteStudyMaterial(string id)
        {
            try
            {
                var result = await _studyMaterialService.DeleteStudyMaterialAsync(id);
                if (result)
                {
                    return Ok(_responseHelper.Success<object>(null, "Study material deleted successfully"));
                }
                return BadRequest(_responseHelper.BadRequest<object>("Failed to delete study material"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
        }
    }
}

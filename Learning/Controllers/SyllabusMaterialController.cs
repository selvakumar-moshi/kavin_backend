using System.Security.Claims;
using LearningBackendAPI.DTOs;
using LearningBackendAPI.Helpers;
using LearningBackendAPI.Services;
using LearningBackendAPI.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearningBackendAPI.Controllers
{
    /// <summary>
    /// PDF materials filed under a folder / sub folder (see /api/Folder). The admin manages them; a student sees only
    /// the materials their verified syllabus wise payment covers (see /api/SyllabusWise).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SyllabusMaterialController : BusinessControllerBase
    {
        private readonly ISyllabusMaterialService _service;

        public SyllabusMaterialController(ISyllabusMaterialService service, ResponseHelper responseHelper) : base(responseHelper)
        {
            _service = service;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        private bool IsAdmin => User.FindFirstValue(ClaimTypes.Role) == Constants.Roles.Admin;

        /// <summary>Form data: title, description?, folderId, subFolderId (needed when the folder has sub folders), file (PDF, max 10 MB) (Admin only)</summary>
        [HttpPost]
        [Authorize(Roles = Constants.Roles.Admin)]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public Task<IActionResult> Create([FromForm] SyllabusMaterialRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.CreateAsync(request), "Syllabus material created successfully")));

        /// <summary>
        /// List materials. Admin sees all; a student sees only what they have paid for. Body or query:
        /// { searchTerm, folderId, subFolderId, pageNumber, pageSize } - a sub folder also matches everything below it.
        /// </summary>
        [HttpPost("search")]
        public Task<IActionResult> Search([FromBody] SyllabusMaterialSearchRequest? request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.SearchAsync(CurrentUserId, IsAdmin, request), "Syllabus materials retrieved successfully")));

        [HttpGet]
        public Task<IActionResult> GetAll([FromQuery] SyllabusMaterialSearchRequest? request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.SearchAsync(CurrentUserId, IsAdmin, request), "Syllabus materials retrieved successfully")));

        [HttpGet("{id}")]
        public Task<IActionResult> GetById(string id) => HandleAsync(async () =>
        {
            try
            {
                return Ok(_responseHelper.Success(await _service.GetByIdAsync(id, CurrentUserId, IsAdmin), "Syllabus material retrieved successfully"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, _responseHelper.Forbidden<object>(ex.Message));
            }
        });

        /// <summary>Form data: title?, description?, folderId?, subFolderId?, file? - only what is sent changes; a new file replaces the old one (Admin only)</summary>
        [HttpPut("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public Task<IActionResult> Update(string id, [FromForm] SyllabusMaterialRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.UpdateAsync(id, request), "Syllabus material updated successfully")));

        [HttpDelete("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> Delete(string id) => HandleAsync(async () =>
        {
            await _service.DeleteAsync(id);
            return Ok(_responseHelper.Success<object?>(null, "Syllabus material deleted successfully"));
        });
    }
}

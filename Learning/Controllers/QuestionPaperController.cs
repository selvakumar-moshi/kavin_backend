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
    public class QuestionPaperController : ControllerBase
    {
        private readonly IQuestionPaperService _questionPaperService;
        private readonly ResponseHelper _responseHelper;

        public QuestionPaperController(
            IQuestionPaperService questionPaperService,
            ResponseHelper responseHelper)
        {
            _questionPaperService = questionPaperService;
            _responseHelper = responseHelper;
        }

        /// <summary>
        /// Create a new question paper with a PDF upload
        /// </summary>
        [HttpPost]
        [Authorize(Roles = Constants.Roles.Admin)]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> CreateQuestionPaper([FromForm] QuestionPaperRequest request)
        {
            try
            {
                var result = await _questionPaperService.CreateQuestionPaperAsync(request);
                return Ok(_responseHelper.Success(result, "Question paper created successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
        }

        /// <summary>
        /// Get question papers the current user has access to (Admin sees all, students see only
        /// verified/paid courses), optionally filtered via a { courseId, material, searchTerm, globalFilter,
        /// pageNumber, pageSize } body payload (courseId: filter to a single course, omit/"All" for every
        /// accessible course; material: informational tag the UI sends, e.g. "study", not used to filter since
        /// this endpoint only ever returns question papers; globalFilter: subset of title/description/batchTitle/materialToView,
        /// all four if omitted; pageNumber/pageSize default to 1/10 if omitted)
        /// </summary>
        [HttpPost("search")]
        public async Task<IActionResult> GetAllQuestionPapers([FromBody] MaterialSearchRequest? request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var role = User.FindFirstValue(ClaimTypes.Role) ?? Constants.Roles.User;
            var globalFilter = request?.GlobalFilter == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(request.GlobalFilter);
            if (!string.IsNullOrWhiteSpace(request?.MaterialToView) &&
                !string.Equals(request.MaterialToView, "All", StringComparison.OrdinalIgnoreCase))
            {
                globalFilter["materialToView"] = request.MaterialToView;
            }
            var questionPapers = await _questionPaperService.GetAccessibleQuestionPapersAsync(
                userId, role, request?.CourseId, request?.SearchTerm, globalFilter.Count > 0 ? globalFilter : null, request?.PageNumber ?? 1, request?.PageSize ?? 10);
            return Ok(_responseHelper.Success(questionPapers, "Question papers retrieved successfully"));
        }

        /// <summary>
        /// Get question paper by ID (only accessible if the current user's course payment is verified, or is Admin)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetQuestionPaperById(string id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                var role = User.FindFirstValue(ClaimTypes.Role) ?? Constants.Roles.User;
                var questionPaper = await _questionPaperService.GetQuestionPaperByIdAsync(id, userId, role);
                return Ok(_responseHelper.Success(questionPaper, "Question paper retrieved successfully"));
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
        /// Update question paper (optionally replacing the PDF)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> UpdateQuestionPaper(string id, [FromForm] QuestionPaperRequest request)
        {
            try
            {
                var questionPaper = await _questionPaperService.UpdateQuestionPaperAsync(id, request);
                return Ok(_responseHelper.Success(questionPaper, "Question paper updated successfully"));
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
        /// Delete question paper
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> DeleteQuestionPaper(string id)
        {
            try
            {
                var result = await _questionPaperService.DeleteQuestionPaperAsync(id);
                if (result)
                {
                    return Ok(_responseHelper.Success<object>(null, "Question paper deleted successfully"));
                }
                return BadRequest(_responseHelper.BadRequest<object>("Failed to delete question paper"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
        }
    }
}

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
    public class QuizController : ControllerBase
    {
        private readonly IQuizService _quizService;
        private readonly ResponseHelper _responseHelper;

        public QuizController(
            IQuizService quizService,
            ResponseHelper responseHelper)
        {
            _quizService = quizService;
            _responseHelper = responseHelper;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        private bool IsAdmin => User.FindFirstValue(ClaimTypes.Role) == Constants.Roles.Admin;

        /// <summary>
        /// Create a new quiz as a Draft with manually-entered questions (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = Constants.Roles.Admin)]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> CreateQuiz([FromForm] QuizCreateRequest request)
        {
            try
            {
                var quiz = await _quizService.CreateQuizAsync(request);
                return Ok(_responseHelper.Success(quiz, "Quiz created successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
        }

        /// <summary>
        /// Edit a quiz's title/questions/correct answers (Admin only). Draft quizzes are edited in place.
        /// Editing a published quiz takes it offline (back to Draft, clearing its publish/expiry state) -
        /// call publish again afterwards, with a new expiry, to make the updated version live.
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> UpdateQuiz(string id, [FromForm] QuizUpdateRequest request)
        {
            try
            {
                var quiz = await _quizService.UpdateQuizAsync(id, request);
                return Ok(_responseHelper.Success(quiz, "Quiz updated successfully"));
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
        /// Publish a draft quiz, making it visible to students until the given expiry date/time (Admin only)
        /// </summary>
        [HttpPost("{id}/publish")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> PublishQuiz(string id, [FromBody] QuizPublishRequest request)
        {
            try
            {
                var quiz = await _quizService.PublishQuizAsync(id, request?.ExpiresAt ?? default);
                return Ok(_responseHelper.Success(quiz, "Quiz published successfully"));
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
        /// Delete a draft quiz (Admin only, published quizzes cannot be deleted)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> DeleteQuiz(string id)
        {
            try
            {
                var result = await _quizService.DeleteQuizAsync(id);
                if (result)
                {
                    return Ok(_responseHelper.Success<object>(null, "Quiz deleted successfully"));
                }
                return BadRequest(_responseHelper.BadRequest<object>("Failed to delete quiz"));
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
        /// Get quizzes: Admin sees every quiz with answer keys; students see only published,
        /// non-expired quizzes for courses they've paid for, with answer keys stripped.
        /// Optionally filtered via a { courseId, pageNumber, pageSize } body payload
        /// (pageNumber/pageSize default to 1/10 if omitted).
        /// </summary>
        [HttpPost("search")]
        public async Task<IActionResult> GetAllQuizzes([FromBody] QuizSearchRequest? request)
        {
            try
            {
                if (IsAdmin)
                {
                    var quizzes = await _quizService.GetAllQuizzesForAdminAsync(request?.PageNumber ?? 1, request?.PageSize ?? 10);
                    return Ok(_responseHelper.Success(quizzes, "Quizzes retrieved successfully"));
                }

                var studentQuizzes = await _quizService.GetAccessibleQuizzesForStudentAsync(
                    CurrentUserId, request?.CourseId, request?.PageNumber ?? 1, request?.PageSize ?? 10);
                return Ok(_responseHelper.Success(studentQuizzes, "Quizzes retrieved successfully"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, _responseHelper.Forbidden<object>(ex.Message));
            }
        }

        /// <summary>
        /// Get a quiz by ID (Admin sees answer key, students do not until they submit)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetQuizById(string id)
        {
            try
            {
                if (IsAdmin)
                {
                    var quiz = await _quizService.GetQuizByIdForAdminAsync(id);
                    return Ok(_responseHelper.Success(quiz, "Quiz retrieved successfully"));
                }

                var studentQuiz = await _quizService.GetQuizByIdForStudentAsync(id, CurrentUserId);
                return Ok(_responseHelper.Success(studentQuiz, "Quiz retrieved successfully"));
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
        /// Submit answers for a published quiz (one attempt per student)
        /// </summary>
        [HttpPost("{id}/submit")]
        public async Task<IActionResult> SubmitQuiz(string id, [FromBody] QuizSubmitRequest request)
        {
            try
            {
                var result = await _quizService.SubmitQuizAsync(id, CurrentUserId, request);
                return Ok(_responseHelper.Success(result, "Quiz submitted successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, _responseHelper.Forbidden<object>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
        }

        /// <summary>
        /// Get the current student's own result for a quiz they've already submitted
        /// </summary>
        [HttpGet("{id}/my-result")]
        public async Task<IActionResult> GetMyResult(string id)
        {
            try
            {
                var result = await _quizService.GetMyResultAsync(id, CurrentUserId);
                return Ok(_responseHelper.Success(result, "Result retrieved successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
        }

        /// <summary>
        /// Get the rank list (leaderboard) for a quiz
        /// </summary>
        [HttpGet("{id}/rank-list")]
        public async Task<IActionResult> GetRankList(string id)
        {
            try
            {
                var rankList = await _quizService.GetRankListAsync(id, CurrentUserId, IsAdmin);
                return Ok(_responseHelper.Success(rankList, "Rank list retrieved successfully"));
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
        /// Download the rank list (leaderboard) for a quiz as an Excel (.xlsx) file (Admin only)
        /// </summary>
        [HttpGet("{id}/rank-list/download")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> DownloadRankList(string id)
        {
            try
            {
                var (content, fileName) = await _quizService.ExportRankListAsync(id, CurrentUserId, IsAdmin);
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
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
    }
}

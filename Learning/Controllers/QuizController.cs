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
        private static readonly System.Text.Json.JsonSerializerOptions PassageJsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

        // The admin quiz as stored, except "questions" is rearranged: a regular question stays as it is, and the
        // questions of one passage are gathered into a single entry { passageQuiz, questions[] } placed where the
        // passage's first question was. Authored order is kept; the entry is told apart by its "questions" array.
        private static System.Text.Json.Nodes.JsonNode? WithPassageGroups(Models.Quiz quiz)
        {
            var node = System.Text.Json.JsonSerializer.SerializeToNode(quiz, PassageJsonOptions);
            if (node is not System.Text.Json.Nodes.JsonObject root)
            {
                return node;
            }

            var entries = new List<object>();
            var groups = new Dictionary<string, List<Models.QuizQuestionItem>>();
            foreach (var q in quiz.Questions.OrderBy(q => q.QuestionNumber))
            {
                if (string.IsNullOrWhiteSpace(q.PassageQuiz))
                {
                    entries.Add(q);
                    continue;
                }

                var key = q.PassageQuiz.Trim();
                if (!groups.TryGetValue(key, out var members))
                {
                    members = new List<Models.QuizQuestionItem>();
                    groups[key] = members;
                    entries.Add(new { passageQuiz = key, questions = members });
                }
                members.Add(q);
            }

            root["questions"] = System.Text.Json.JsonSerializer.SerializeToNode(entries, PassageJsonOptions);
            return root;
        }

        private static QuizClassification? ToClassificationFilter(QuizSearchRequest? request)
        {
            if (request == null)
            {
                return null;
            }

            return new QuizClassification
            {
                QuizType = string.IsNullOrWhiteSpace(request.QuizType) ? null : (Constants.QuizTypes.Normalize(request.QuizType) ?? request.QuizType.Trim()),
                QuizToView = string.IsNullOrWhiteSpace(request.QuizToView) ? null : Constants.MaterialAccess.Normalize(request.QuizToView),
                FolderId = string.IsNullOrWhiteSpace(request.FolderId) ? null : request.FolderId.Trim(),
                SubFolderId = string.IsNullOrWhiteSpace(request.SubFolderId) ? null : request.SubFolderId.Trim()
            };
        }

        private bool IsAdmin => User.FindFirstValue(ClaimTypes.Role) == Constants.Roles.Admin;

        /// <summary>
        /// Create a new quiz as a Draft with manually-entered questions (Admin only). Each question
        /// accepts a "mark" (defaults to 1 if omitted); the rank list and a student's score are
        /// based on marks scored, not just the count of correct answers. quizToView is "Paid"
        /// (default - requires a verified enrollment in the quiz's course) or "Free" (visible and
        /// attemptable by any student regardless of course purchase). Instead of typing questions, pass a
        /// Word (.docx) in the "file" field to read them from the document (1 mark each) - preview the file
        /// first with POST /api/Quiz/import; questions that can't be read are left out.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = Constants.Roles.Admin)]
        [RequestSizeLimit(20 * 1024 * 1024)]
        [RequestFormLimits(ValueCountLimit = 20000)] // every question sends ~10 form fields; the 1024 default caps a quiz at ~100 questions
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
        /// Read the questions out of a Word (.docx) file and return them, WITHOUT creating a quiz (Admin
        /// only) - use it to check the file before creating the quiz. Layout expected: each question
        /// starts on a new line with its number ("1. Question text"), followed by A) B) C) D) options
        /// (D is optional), with the correct option marked with a tick. "(விளக்கம்: ...)" explanations
        /// are ignored. Questions that can't be read are listed in "skipped" and doubtful ones in
        /// "warnings", using the numbers from the document. To create the quiz, call POST /api/Quiz
        /// with the same file in its "file" field. Form field: file.
        /// </summary>
        [HttpPost("import")]
        [Authorize(Roles = Constants.Roles.Admin)]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> ImportQuiz([FromForm] QuizImportRequest request)
        {
            try
            {
                var result = await _quizService.PreviewDocxQuizAsync(request.File);
                return Ok(_responseHelper.Success(result, $"{result.Readable} of {result.TotalFoundInDocument} questions read from the document"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
        }

        /// <summary>
        /// Copy an existing quiz (questions, answers, marks, images) as a new Draft (Admin only). The copy can
        /// be Paid (pass batchId - a batch of the quiz's course) or Free (no batchId needed); quizToView
        /// defaults to the original's. The original quiz and its results are untouched; publish
        /// the copy with its own expiry. Body: { quizId, quizToView (optional), batchId (required for Paid), title (optional - defaults to the original title) }.
        /// </summary>
        [HttpPost("copy")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> CopyQuiz([FromBody] QuizCopyRequest request)
        {
            try
            {
                var quiz = await _quizService.CopyQuizAsync(request);
                return Ok(_responseHelper.Success(quiz, "Quiz copied successfully"));
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
        /// Edit a quiz's title/questions/correct answers (Admin only). Draft quizzes are edited in place.
        /// Editing a published quiz takes it offline (back to Draft, clearing its publish/expiry state) -
        /// call publish again afterwards, with a new expiry, to make the updated version live.
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        [RequestSizeLimit(20 * 1024 * 1024)]
        [RequestFormLimits(ValueCountLimit = 20000)]
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
        /// Publish a draft quiz (Admin only). Body, every field optional: { startDate, endDate, expireAt, shuffleQuestions? }.
        /// expireAt alone = live now until then. startDate / endDate = live only in that window (before the start students see
        /// the quiz but can't open the questions or submit). Nothing = live now with no end. expireAt can't be combined with
        /// startDate / endDate. Times without a Z / offset are the institute's local time.
        /// </summary>
        [HttpPost("{id}/publish")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> PublishQuiz(string id, [FromBody] QuizPublishRequest request)
        {
            try
            {
                var quiz = await _quizService.PublishQuizAsync(id, request?.StartDate, request?.EndDate, request?.ExpireAt, request?.ShuffleQuestions ?? true);
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
        /// Delete a quiz, Draft or Published (Admin only). Students' submitted results are kept; the quiz's
        /// question images are removed only if no student has attempted it.
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
        /// Get quizzes: Admin sees every quiz with answer keys. Students see published, non-expired
        /// quizzes with answer keys stripped - "Paid" quizzes only for courses they've verified-paid
        /// for, "Free" quizzes regardless of purchase; passing a courseId no longer requires the
        /// student to be enrolled in it (they'll just see whatever Free quizzes exist for that course
        /// if they aren't). Optionally filtered via a { courseId, searchTerm, globalFilter, pageNumber,
        /// pageSize } body payload (searchTerm: matches quiz title or course name; globalFilter:
        /// subset of title/courseName/status, all three if omitted - note students never see Draft
        /// quizzes regardless of a status filter, since that list is always pre-restricted to
        /// Published; pageNumber/pageSize default to 1/10 if omitted).
        /// </summary>
        [HttpPost("search")]
        public async Task<IActionResult> GetAllQuizzes([FromBody] QuizSearchRequest? request)
        {
            try
            {
                if (IsAdmin)
                {
                    var quizzes = await _quizService.GetAllQuizzesForAdminAsync(
                        request?.SearchTerm, request?.GlobalFilter, request?.PageNumber ?? 1, request?.PageSize ?? 10, ToClassificationFilter(request));
                    return Ok(_responseHelper.Success(quizzes, "Quizzes retrieved successfully"));
                }

                var studentQuizzes = await _quizService.GetAccessibleQuizzesForStudentAsync(
                    CurrentUserId, request?.CourseId, request?.SearchTerm, request?.GlobalFilter, request?.PageNumber ?? 1, request?.PageSize ?? 10, ToClassificationFilter(request));
                return Ok(_responseHelper.Success(studentQuizzes, "Quizzes retrieved successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
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
                    return Ok(_responseHelper.Success(WithPassageGroups(quiz), "Quiz retrieved successfully"));
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
        /// Get the rank list (leaderboard) for a quiz. Admin filters (JSON body or query string; body wins):
        /// batchId, quizType (must match the quiz), quizToView ("Paid" = paid students, "Free" = unpaid students).
        /// </summary>
        [HttpGet("{id}/rank-list")]
        public async Task<IActionResult> GetRankList(string id,
            [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RankListRequest? body,
            [FromQuery] RankListRequest? query)
        {
            try
            {
                var rankList = await _quizService.GetRankListAsync(id, CurrentUserId, IsAdmin, MergeRankListRequest(body, query));
                return Ok(_responseHelper.Success(rankList, "Rank list retrieved successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, _responseHelper.Forbidden<object>(ex.Message));
            }
        }

        /// <summary>
        /// Download the rank list (leaderboard) for a quiz as an Excel (.xlsx) file (Admin only).
        /// Takes the same filters as the rank list (batchId, quizType, quizToView).
        /// </summary>
        [HttpGet("{id}/rank-list/download")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> DownloadRankList(string id,
            [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RankListRequest? body,
            [FromQuery] RankListRequest? query)
        {
            try
            {
                var (content, fileName) = await _quizService.ExportRankListAsync(id, CurrentUserId, IsAdmin, MergeRankListRequest(body, query));
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, _responseHelper.Forbidden<object>(ex.Message));
            }
        }

        // GET bodies are dropped by some clients, so every filter also works as a query param
        private static RankListRequest MergeRankListRequest(RankListRequest? body, RankListRequest? query) => new()
        {
            BatchId = body?.BatchId ?? query?.BatchId,
            QuizType = body?.QuizType ?? query?.QuizType,
            QuizToView = body?.QuizToView ?? query?.QuizToView
        };
    }
}

using LearningBackendAPI.DTOs;
using LearningBackendAPI.Helpers;
using LearningBackendAPI.Services;
using LearningBackendAPI.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearningBackendAPI.Controllers
{
    /// <summary>
    /// Manage the Subject → (Category) → Standard → (Part) options used to classify quizzes. Subjects,
    /// categories, standards and parts are created separately, then linked by value: a subject (or each of
    /// its categories) lists the standards it offers and, for each, the parts it has (Tamil std 6 can have
    /// Part-1..3 while Maths std 6 has none). Reading is open to any
    /// signed-in user; creating, updating and deleting is Admin only. Items that quizzes already use
    /// can't be renamed or deleted. The assembled tree is GET /api/Quiz/categories.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class QuizStructureController : BusinessControllerBase
    {
        private readonly IQuizStructureService _service;

        public QuizStructureController(IQuizStructureService service, ResponseHelper responseHelper) : base(responseHelper)
        {
            _service = service;
        }

        // ---------- Subjects ----------

        [HttpGet("subjects")]
        public Task<IActionResult> GetSubjects() => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.GetSubjectsAsync(), "Subjects retrieved successfully")));

        /// <summary>
        /// Body: { name, standards? } - standards as [{ "standard": 6, "parts": ["Part-1"] }, { "standard": 12 }]; a standard
        /// without parts has none for this subject. Only for a subject without categories (e.g. Tamil).
        /// </summary>
        [HttpPost("subjects")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> CreateSubject([FromBody] QuizSubjectRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.CreateSubjectAsync(request), "Subject created successfully")));

        [HttpPut("subjects/{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> UpdateSubject(string id, [FromBody] QuizSubjectRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.UpdateSubjectAsync(id, request), "Subject updated successfully")));

        [HttpDelete("subjects/{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> DeleteSubject(string id) => HandleAsync(async () =>
        {
            await _service.DeleteSubjectAsync(id);
            return Ok(_responseHelper.Success<object?>(null, "Subject deleted successfully"));
        });

        // ---------- Categories ----------

        /// <summary>Optional ?subject=GK to list one subject's categories.</summary>
        [HttpGet("categories")]
        public Task<IActionResult> GetCategories([FromQuery] string? subject) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.GetCategoriesAsync(subject), "Categories retrieved successfully")));

        /// <summary>Body: { name, subject, standards? } - subject by name; standards as [{ "standard": 6, "parts": ["Part-1"] }, { "standard": 12 }].</summary>
        [HttpPost("categories")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> CreateCategory([FromBody] QuizCategoryRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.CreateCategoryAsync(request), "Category created successfully")));

        [HttpPut("categories/{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> UpdateCategory(string id, [FromBody] QuizCategoryRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.UpdateCategoryAsync(id, request), "Category updated successfully")));

        [HttpDelete("categories/{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> DeleteCategory(string id) => HandleAsync(async () =>
        {
            await _service.DeleteCategoryAsync(id);
            return Ok(_responseHelper.Success<object?>(null, "Category deleted successfully"));
        });

        // ---------- Standards ----------

        [HttpGet("standards")]
        public Task<IActionResult> GetStandards() => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.GetStandardsAsync(), "Standards retrieved successfully")));

        /// <summary>Body: { standard } - e.g. { "standard": 6 }. Parts are not set here but per subject/category.</summary>
        [HttpPost("standards")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> CreateStandard([FromBody] QuizStandardRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.CreateStandardAsync(request), "Standard created successfully")));

        [HttpPut("standards/{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> UpdateStandard(string id, [FromBody] QuizStandardRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.UpdateStandardAsync(id, request), "Standard updated successfully")));

        [HttpDelete("standards/{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> DeleteStandard(string id) => HandleAsync(async () =>
        {
            await _service.DeleteStandardAsync(id);
            return Ok(_responseHelper.Success<object?>(null, "Standard deleted successfully"));
        });

        // ---------- Parts ----------

        [HttpGet("parts")]
        public Task<IActionResult> GetParts() => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.GetPartsAsync(), "Parts retrieved successfully")));

        /// <summary>Body: { name }.</summary>
        [HttpPost("parts")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> CreatePart([FromBody] QuizPartRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.CreatePartAsync(request), "Part created successfully")));

        [HttpPut("parts/{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> UpdatePart(string id, [FromBody] QuizPartRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.UpdatePartAsync(id, request), "Part updated successfully")));

        [HttpDelete("parts/{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> DeletePart(string id) => HandleAsync(async () =>
        {
            await _service.DeletePartAsync(id);
            return Ok(_responseHelper.Success<object?>(null, "Part deleted successfully"));
        });
    }
}

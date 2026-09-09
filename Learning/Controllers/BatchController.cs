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
    public class BatchController : ControllerBase
    {
        private readonly IBatchService _batchService;
        private readonly ResponseHelper _responseHelper;

        public BatchController(
            IBatchService batchService,
            ResponseHelper responseHelper)
        {
            _batchService = batchService;
            _responseHelper = responseHelper;
        }

        /// <summary>
        /// Create a new batch mapped to a course (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> CreateBatch([FromBody] BatchCreateRequest request)
        {
            try
            {
                var batch = await _batchService.CreateBatchAsync(request);
                return Ok(_responseHelper.Success(batch, "Batch created successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
        }

        /// <summary>
        /// Update a batch (Admin only)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> UpdateBatch(string id, [FromBody] BatchUpdateRequest request)
        {
            try
            {
                var batch = await _batchService.UpdateBatchAsync(id, request);
                return Ok(_responseHelper.Success(batch, "Batch updated successfully"));
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
        /// Get batches, optionally filtered via a { courseId, searchTerm, globalFilter, pageNumber, pageSize }
        /// body payload (globalFilter: subset of title/courseName). Admin (authenticated) sees all
        /// batches including past ones; everyone else (including anonymous callers during registration)
        /// only sees current/future, non-expired batches. pageNumber/pageSize default to 1/10 if omitted.
        /// </summary>
        [HttpPost("search")]
        [AllowAnonymous]
        public async Task<IActionResult> GetBatches([FromBody] BatchSearchRequest? request)
        {
            var isAdmin = User.FindFirstValue(ClaimTypes.Role) == Constants.Roles.Admin;
            var batches = await _batchService.GetBatchesAsync(
                request?.CourseId, includeExpired: isAdmin, request?.SearchTerm, request?.GlobalFilter,
                request?.PageNumber ?? 1, request?.PageSize ?? 10);
            return Ok(_responseHelper.Success(batches, "Batches retrieved successfully"));
        }

        /// <summary>
        /// Delete a batch (Admin only)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> DeleteBatch(string id)
        {
            try
            {
                var result = await _batchService.DeleteBatchAsync(id);
                if (result)
                {
                    return Ok(_responseHelper.Success<object>(null, "Batch deleted successfully"));
                }
                return BadRequest(_responseHelper.BadRequest<object>("Failed to delete batch"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
        }
    }
}

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
    public class EnrollmentController : ControllerBase
    {
        private readonly IEnrollmentService _enrollmentService;
        private readonly ResponseHelper _responseHelper;

        public EnrollmentController(
            IEnrollmentService enrollmentService,
            ResponseHelper responseHelper)
        {
            _enrollmentService = enrollmentService;
            _responseHelper = responseHelper;
        }

        /// <summary>
        /// Get all enrollments (Admin only)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> GetAllEnrollments()
        {
            var enrollments = await _enrollmentService.GetAllEnrollmentsAsync();
            return Ok(_responseHelper.Success(enrollments, "Enrollments retrieved successfully"));
        }

        /// <summary>
        /// Get the current logged-in student's own enrollments
        /// </summary>
        [HttpGet("my")]
        public async Task<IActionResult> GetMyEnrollments()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var enrollments = await _enrollmentService.GetEnrollmentsByUserIdAsync(userId!);
            return Ok(_responseHelper.Success(enrollments, "Enrollments retrieved successfully"));
        }

        /// <summary>
        /// Get enrollment by ID (Admin only)
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> GetEnrollmentById(string id)
        {
            try
            {
                var enrollment = await _enrollmentService.GetEnrollmentByIdAsync(id);
                return Ok(_responseHelper.Success(enrollment, "Enrollment retrieved successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
        }

        /// <summary>
        /// Update the status of an enrollment (Admin only): Verified (e.g. verify offline payment),
        /// Pending (revert an incorrect verification - clears verifiedAt/verifiedByAdminId), or
        /// Dropped (student left this batch - verification history is kept, and they can later
        /// rejoin a different batch for the same course via PUT /api/User/{id} without re-paying)
        /// </summary>
        [HttpPut("{id}/status")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> UpdateEnrollmentStatus(string id, [FromBody] UpdateEnrollmentStatusRequest request)
        {
            try
            {
                var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var enrollment = await _enrollmentService.UpdateStatusAsync(
                    id, request.Status ?? "", request.PaymentMethod, request.TransactionReference, adminUserId!);
                return Ok(_responseHelper.Success(enrollment, "Enrollment status updated successfully"));
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
    }
}

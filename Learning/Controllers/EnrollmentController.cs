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
        private readonly IConfiguration _configuration;

        public EnrollmentController(
            IEnrollmentService enrollmentService,
            ResponseHelper responseHelper,
            IConfiguration configuration)
        {
            _enrollmentService = enrollmentService;
            _responseHelper = responseHelper;
            _configuration = configuration;
        }

        /// <summary>
        /// UPI ID and payee name the mobile app pays to (appsettings "Payment" section), so the
        /// UPI ID can be changed without shipping a new app.
        /// </summary>
        [HttpGet("payment-info")]
        public IActionResult GetPaymentInfo()
        {
            var info = new PaymentInfoResponse
            {
                UpiId = _configuration["Payment:UpiId"] ?? "",
                PayeeName = _configuration["Payment:PayeeName"] ?? ""
            };
            if (string.IsNullOrWhiteSpace(info.UpiId))
            {
                return BadRequest(_responseHelper.BadRequest<object>(Constants.Messages.UpiNotConfigured));
            }
            return Ok(_responseHelper.Success(info, "Payment info retrieved successfully"));
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
        /// Student picks a course and batch to enroll in. Creates a Pending enrollment - the student
        /// then uploads the payment screenshot and the admin verifies it. Body: { courseId, batchId }.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Enroll([FromBody] EnrollRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                var enrollment = await _enrollmentService.EnrollAsync(userId, request.CourseId, request.BatchId);
                return Ok(_responseHelper.Success(enrollment, "Enrolled successfully. Please upload your payment screenshot."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
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

        /// <summary>
        /// Upload (or replace) the GPay/PhonePe payment screenshot for the current student's own
        /// enrollment, together with its UPI transaction ID (multipart/form-data: file, transactionId). Stored in S3 under
        /// coaching/payment; the admin sees the screenshot and the transaction ID on the enrollment. A transaction ID that was
        /// already used for another payment is refused: "This transaction ID has already been used for a payment."
        /// </summary>
        [HttpPost("{id}/payment-attachment")]
        [RequestSizeLimit(5 * 1024 * 1024)]
        public async Task<IActionResult> UploadPaymentAttachment(string id, [FromForm] PaymentAttachmentRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                var enrollment = await _enrollmentService.UploadPaymentAttachmentAsync(id, userId, request.File!, request.TransactionId);
                return Ok(_responseHelper.Success(enrollment, "Payment screenshot uploaded successfully"));
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
        /// Download an Excel (.xlsx) report of every student enrolled in a course, including their
        /// batch and enrollment status/dates (Admin only)
        /// </summary>
        [HttpGet("course/{courseId}/download")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> DownloadCourseEnrollments(string courseId)
        {
            try
            {
                var (content, fileName) = await _enrollmentService.ExportCourseEnrollmentsAsync(courseId);
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
        }
    }
}

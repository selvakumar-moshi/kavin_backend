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
    /// The paid "syllabusWise" quiz plan (title, description, amount) and the students' payments for it.
    /// A student with a Verified payment can open the paid quizzes whose quizType is syllabusWise.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SyllabusWiseController : BusinessControllerBase
    {
        private readonly ISyllabusWiseService _service;

        public SyllabusWiseController(ISyllabusWiseService service, ResponseHelper responseHelper) : base(responseHelper)
        {
            _service = service;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        /// <summary>Every plan - students use it to show what they are paying for.</summary>
        [HttpGet]
        public Task<IActionResult> GetPlans() => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.GetPlansAsync(), "Plans retrieved successfully")));

        [HttpGet("{id}")]
        public Task<IActionResult> GetPlan(string id) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.GetPlanAsync(id), "Plan retrieved successfully")));

        /// <summary>Body: { title, description?, amount } (Admin only)</summary>
        [HttpPost]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> CreatePlan([FromBody] SyllabusWisePlanRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.CreatePlanAsync(request), "Plan created successfully")));

        /// <summary>Body: { title?, description?, amount? } - only what is sent changes (Admin only)</summary>
        [HttpPut("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> UpdatePlan(string id, [FromBody] SyllabusWisePlanRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.UpdatePlanAsync(id, request), "Plan updated successfully")));

        [HttpDelete("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> DeletePlan(string id) => HandleAsync(async () =>
        {
            await _service.DeletePlanAsync(id);
            return Ok(_responseHelper.Success<object?>(null, "Plan deleted successfully"));
        });

        /// <summary>
        /// The signed-in student reports their payment. Body: { paymentStatus: "verified", userId?, planId?, paymentMethod?,
        /// transactionReference? }. The student is taken from the token - userId is optional and rejected if it isn't theirs.
        /// planId can be left out while there is only one plan. Sending it again updates the same payment; a payment that is
        /// already Verified is left as it is.
        /// </summary>
        [HttpPost("payment")]
        public Task<IActionResult> SubmitPayment([FromBody] SyllabusWisePaymentRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.SubmitPaymentAsync(CurrentUserId, request), "Payment saved successfully")));

        /// <summary>Payments of every student, newest first; ?userId= narrows it to one student (Admin only).</summary>
        [HttpGet("payments")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> GetPayments([FromQuery] string? userId) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.GetPaymentsAsync(userId), "Payments retrieved successfully")));

        /// <summary>Body: { paymentStatus: "pending" | "verified" | "rejected" } - lets the admin confirm or revoke a payment (Admin only).</summary>
        [HttpPut("payments/{id}/status")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> UpdatePaymentStatus(string id, [FromBody] SyllabusWisePaymentStatusRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.UpdatePaymentStatusAsync(id, request), "Payment status updated successfully")));
    }
}

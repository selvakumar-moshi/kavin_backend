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
    /// Scheduled live classes. Status is automatic: Not Started, then Online from startTime, then Expired at endTime.
    /// Expired classes are hidden from students and kept for the admin.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LiveClassesController : BusinessControllerBase
    {
        private readonly ILiveClassService _service;

        public LiveClassesController(ILiveClassService service, ResponseHelper responseHelper) : base(responseHelper)
        {
            _service = service;
        }

        private bool IsAdmin => User.FindFirstValue(ClaimTypes.Role) == Constants.Roles.Admin;

        /// <summary>Body: { title, description?, date, startTime, endTime } (Admin only). Status starts as "Not Started".</summary>
        [HttpPost]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> Create([FromBody] LiveClassRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.CreateAsync(request), "Live class created successfully")));

        /// <summary>Admin sees every live class; students don't get the Expired ones.</summary>
        [HttpGet]
        public Task<IActionResult> GetAll() => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.GetAllAsync(IsAdmin), "Live classes retrieved successfully")));

        [HttpGet("{id}")]
        public Task<IActionResult> GetById(string id) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.GetByIdAsync(id, IsAdmin), "Live class retrieved successfully")));

        /// <summary>Only the fields that are sent change (Admin only).</summary>
        [HttpPut("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> Update(string id, [FromBody] LiveClassRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _service.UpdateAsync(id, request), "Live class updated successfully")));

        [HttpDelete("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> Delete(string id) => HandleAsync(async () =>
        {
            await _service.DeleteAsync(id);
            return Ok(_responseHelper.Success<object?>(null, "Live class deleted successfully"));
        });
    }
}

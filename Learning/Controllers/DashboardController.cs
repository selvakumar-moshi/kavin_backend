using LearningBackendAPI.Helpers;
using LearningBackendAPI.Services;
using LearningBackendAPI.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearningBackendAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Constants.Roles.Admin)]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly ResponseHelper _responseHelper;

        public DashboardController(
            IDashboardService dashboardService,
            ResponseHelper responseHelper)
        {
            _dashboardService = dashboardService;
            _responseHelper = responseHelper;
        }

        /// <summary>
        /// Get dashboard counts (Admin only)
        /// </summary>
        [HttpGet("counts")]
        public async Task<IActionResult> GetCounts()
        {
            var counts = await _dashboardService.GetCountsAsync();
            return Ok(_responseHelper.Success(counts, "Dashboard counts retrieved successfully"));
        }
    }
}

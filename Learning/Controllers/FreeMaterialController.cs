using LearningBackendAPI.DTOs;
using LearningBackendAPI.Helpers;
using LearningBackendAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace LearningBackendAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FreeMaterialController : ControllerBase
    {
        private readonly IFreeMaterialService _freeMaterialService;
        private readonly ResponseHelper _responseHelper;

        public FreeMaterialController(
            IFreeMaterialService freeMaterialService,
            ResponseHelper responseHelper)
        {
            _freeMaterialService = freeMaterialService;
            _responseHelper = responseHelper;
        }

        /// <summary>
        /// Get all materials marked "Free" (study and video combined, each tagged with a
        /// materialType field) - publicly accessible, no login required. Optionally filtered via
        /// a { courseId, searchTerm } body payload (courseId: omit/"All" for every course;
        /// searchTerm: matches title/description/batchTitle). Returns the full unpaginated list.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GetFreeMaterials([FromBody] FreeMaterialSearchRequest? request)
        {
            var materials = await _freeMaterialService.GetFreeMaterialsAsync(request?.CourseId, request?.SearchTerm);
            return Ok(_responseHelper.Success(materials, "Free materials retrieved successfully"));
        }
    }
}

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
    public class CourseController : ControllerBase
    {
        private readonly ICourseService _courseService;
        private readonly ResponseHelper _responseHelper;

        public CourseController(
            ICourseService courseService,
            ResponseHelper responseHelper)
        {
            _courseService = courseService;
            _responseHelper = responseHelper;
        }

        /// <summary>
        /// Create a new course
        /// </summary>
        [HttpPost]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> CreateCourse([FromBody] CourseRequest request)
        {
            try
            {
                var result = await _courseService.CreateCourseAsync(request);
                return Ok(_responseHelper.Success(result, "Course created successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
        }

        /// <summary>
        /// Get all courses
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllCourses()
        {
            var courses = await _courseService.GetAllCoursesAsync();
            return Ok(_responseHelper.Success(courses, "Courses retrieved successfully"));
        }

        /// <summary>
        /// Get course by ID
        /// </summary>
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCourseById(string id)
        {
            try
            {
                var course = await _courseService.GetCourseByIdAsync(id);
                return Ok(_responseHelper.Success(course, "Course retrieved successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
        }

        /// <summary>
        /// Update course
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> UpdateCourse(string id, [FromBody] CourseRequest request)
        {
            try
            {
                var course = await _courseService.UpdateCourseAsync(id, request);
                return Ok(_responseHelper.Success(course, "Course updated successfully"));
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
        /// Delete course
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public async Task<IActionResult> DeleteCourse(string id)
        {
            try
            {
                var result = await _courseService.DeleteCourseAsync(id);
                if (result)
                {
                    return Ok(_responseHelper.Success<object>(null, "Course deleted successfully"));
                }
                return BadRequest(_responseHelper.BadRequest<object>("Failed to delete course"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
        }
    }
}
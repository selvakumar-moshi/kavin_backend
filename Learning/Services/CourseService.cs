using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;

namespace LearningBackendAPI.Services
{
    public class CourseService : ICourseService
    {
        private readonly ICourseRepository _courseRepository;

        public CourseService(ICourseRepository courseRepository)
        {
            _courseRepository = courseRepository;
        }

        public async Task<Course> CreateCourseAsync(CourseRequest request)
        {
            // Handle null values with defaults
            var courseName = string.IsNullOrWhiteSpace(request.CourseName)
                ? ""
                : request.CourseName.Trim();

            var courseDescription = string.IsNullOrWhiteSpace(request.CourseDescription)
                ? ""
                : request.CourseDescription.Trim();

            // Check if course name already exists (only if not empty)
            if (!string.IsNullOrWhiteSpace(courseName) && courseName != "")
            {
                if (await _courseRepository.CourseNameExistsAsync(courseName))
                {
                    throw new InvalidOperationException("Course with this name already exists");
                }
            }

            var courseAmount = request.CourseAmount ?? 0;

            if (courseAmount < 0)
            {
                throw new InvalidOperationException("Course amount cannot be negative");
            }

            var course = new Course
            {
                CourseName = courseName,
                CourseDescription = courseDescription,
                CourseAmount = courseAmount,
                CreatedAt = DateTime.UtcNow
            };

            return await _courseRepository.CreateAsync(course);
        }

        public async Task<List<Course>> GetAllCoursesAsync()
        {
            return await _courseRepository.GetAllAsync();
        }

        public async Task<Course> GetCourseByIdAsync(string id)
        {
            var course = await _courseRepository.GetByIdAsync(id);
            if (course == null)
            {
                throw new KeyNotFoundException("Course not found");
            }
            return course;
        }

        public async Task<Course> UpdateCourseAsync(string id, CourseRequest request)
        {
            var existingCourse = await _courseRepository.GetByIdAsync(id);
            if (existingCourse == null)
            {
                throw new KeyNotFoundException("Course not found");
            }

            // Update only if new values are provided
            if (!string.IsNullOrWhiteSpace(request.CourseName))
            {
                var newName = request.CourseName.Trim();

                // Check if new course name conflicts with another course
                if (existingCourse.CourseName != newName)
                {
                    if (await _courseRepository.CourseNameExistsAsync(newName))
                    {
                        throw new InvalidOperationException("Course with this name already exists");
                    }
                    existingCourse.CourseName = newName;
                }
            }

            if (request.CourseDescription != null)
            {
                existingCourse.CourseDescription = request.CourseDescription.Trim();
            }

            if (request.CourseAmount.HasValue)
            {
                if (request.CourseAmount.Value < 0)
                {
                    throw new InvalidOperationException("Course amount cannot be negative");
                }
                existingCourse.CourseAmount = request.CourseAmount.Value;
            }

            existingCourse.UpdatedAt = DateTime.UtcNow;
            await _courseRepository.UpdateAsync(id, existingCourse);
            return existingCourse;
        }

        public async Task<bool> DeleteCourseAsync(string id)
        {
            var course = await _courseRepository.GetByIdAsync(id);
            if (course == null)
            {
                throw new KeyNotFoundException("Course not found");
            }

            return await _courseRepository.DeleteAsync(id);
        }
    }
}
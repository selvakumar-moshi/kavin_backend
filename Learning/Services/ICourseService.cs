using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;

namespace LearningBackendAPI.Services
{
    public interface ICourseService
    {
        Task<Course> CreateCourseAsync(CourseRequest request);
        Task<List<Course>> GetAllCoursesAsync();
        Task<Course> GetCourseByIdAsync(string id);
        Task<Course> UpdateCourseAsync(string id, CourseRequest request);
        Task<bool> DeleteCourseAsync(string id);
    }
}
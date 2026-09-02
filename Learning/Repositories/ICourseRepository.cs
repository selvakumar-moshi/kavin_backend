using LearningBackendAPI.Models;

namespace LearningBackendAPI.Repositories
{
    public interface ICourseRepository
    {
        Task<Course> GetByIdAsync(string id);
        Task<List<Course>> GetAllAsync();
        Task<Course> CreateAsync(Course course);
        Task UpdateAsync(string id, Course course);
        Task<bool> DeleteAsync(string id);
        Task<bool> CourseNameExistsAsync(string courseName);
    }
}
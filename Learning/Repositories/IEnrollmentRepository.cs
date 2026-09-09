using LearningBackendAPI.Models;

namespace LearningBackendAPI.Repositories
{
    public interface IEnrollmentRepository
    {
        Task<Enrollment> GetByIdAsync(string id);
        Task<List<Enrollment>> GetAllAsync();
        Task<List<Enrollment>> GetByUserIdAsync(string userId);
        Task<Enrollment> CreateAsync(Enrollment enrollment);
        Task UpdateAsync(string id, Enrollment enrollment);
        Task<bool> HasVerifiedEnrollmentAsync(string userId, string courseId);
        Task<List<string>> GetVerifiedCourseIdsAsync(string userId);
    }
}

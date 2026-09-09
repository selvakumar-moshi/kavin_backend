using LearningBackendAPI.Models;

namespace LearningBackendAPI.Repositories
{
    public interface IBatchRepository
    {
        Task<Batch> GetByIdAsync(string id);
        Task<List<Batch>> GetAllAsync();
        Task<long> CountAsync();
        Task<List<Batch>> GetByCourseIdAsync(string courseId);
        Task<Batch> CreateAsync(Batch batch);
        Task UpdateAsync(string id, Batch batch);
        Task<bool> DeleteAsync(string id);
    }
}

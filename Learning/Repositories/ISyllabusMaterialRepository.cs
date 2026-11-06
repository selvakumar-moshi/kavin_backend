using LearningBackendAPI.Models;

namespace LearningBackendAPI.Repositories
{
    public interface ISyllabusMaterialRepository
    {
        Task<List<SyllabusMaterial>> GetAllAsync();
        Task<SyllabusMaterial?> GetByIdAsync(string id);
        Task<SyllabusMaterial> CreateAsync(SyllabusMaterial material);
        Task UpdateAsync(SyllabusMaterial material);
        Task<bool> DeleteAsync(string id);
    }
}

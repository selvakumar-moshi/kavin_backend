using LearningBackendAPI.Models;

namespace LearningBackendAPI.Repositories
{
    public interface ILiveClassRepository
    {
        Task<LiveClass?> GetByIdAsync(string id);
        Task<List<LiveClass>> GetAllAsync();
        Task<LiveClass> CreateAsync(LiveClass liveClass);
        Task UpdateAsync(string id, LiveClass liveClass);
        Task<bool> DeleteAsync(string id);
    }
}

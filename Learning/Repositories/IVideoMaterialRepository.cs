using LearningBackendAPI.Models;

namespace LearningBackendAPI.Repositories
{
    public interface IVideoMaterialRepository
    {
        Task<VideoMaterial> GetByIdAsync(string id);
        Task<List<VideoMaterial>> GetAllAsync();
        Task<long> CountAsync();
        Task<List<VideoMaterial>> GetByCourseIdAsync(string courseId);
        Task<List<VideoMaterial>> GetByCourseIdsAsync(List<string> courseIds);
        Task<VideoMaterial> CreateAsync(VideoMaterial videoMaterial);
        Task UpdateAsync(string id, VideoMaterial videoMaterial);
        Task<bool> DeleteAsync(string id);
    }
}

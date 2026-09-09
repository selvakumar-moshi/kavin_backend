using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;

namespace LearningBackendAPI.Services
{
    public interface IVideoMaterialService
    {
        Task<VideoMaterial> CreateVideoMaterialAsync(VideoMaterialRequest request);
        Task<PagedResult<VideoMaterial>> GetAccessibleVideoMaterialsAsync(string userId, string role, string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize);
        Task<VideoMaterial> GetVideoMaterialByIdAsync(string id, string userId, string role);
        Task<VideoMaterial> UpdateVideoMaterialAsync(string id, VideoMaterialRequest request);
        Task<bool> DeleteVideoMaterialAsync(string id);
    }
}

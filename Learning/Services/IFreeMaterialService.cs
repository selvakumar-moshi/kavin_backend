using LearningBackendAPI.DTOs;

namespace LearningBackendAPI.Services
{
    public interface IFreeMaterialService
    {
        Task<List<FreeMaterialDto>> GetFreeMaterialsAsync(string? courseId, string? searchTerm);
    }
}

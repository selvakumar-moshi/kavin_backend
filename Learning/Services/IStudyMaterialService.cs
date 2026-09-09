using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;

namespace LearningBackendAPI.Services
{
    public interface IStudyMaterialService
    {
        Task<StudyMaterial> CreateStudyMaterialAsync(StudyMaterialRequest request);
        Task<PagedResult<StudyMaterial>> GetAccessibleStudyMaterialsAsync(string userId, string role, string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize);
        Task<StudyMaterial> GetStudyMaterialByIdAsync(string id, string userId, string role);
        Task<StudyMaterial> UpdateStudyMaterialAsync(string id, StudyMaterialRequest request);
        Task<bool> DeleteStudyMaterialAsync(string id);
    }
}

using LearningBackendAPI.Models;

namespace LearningBackendAPI.Repositories
{
    public interface IStudyMaterialRepository
    {
        Task<StudyMaterial> GetByIdAsync(string id);
        Task<List<StudyMaterial>> GetAllAsync();
        Task<long> CountAsync();
        Task<List<StudyMaterial>> GetByCourseIdAsync(string courseId);
        Task<List<StudyMaterial>> GetByCourseIdsAsync(List<string> courseIds);
        Task<StudyMaterial> CreateAsync(StudyMaterial studyMaterial);
        Task UpdateAsync(string id, StudyMaterial studyMaterial);
        Task<bool> DeleteAsync(string id);
    }
}

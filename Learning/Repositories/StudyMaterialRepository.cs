using LearningBackendAPI.Models;
using MongoDB.Driver;

namespace LearningBackendAPI.Repositories
{
    public class StudyMaterialRepository : IStudyMaterialRepository
    {
        private readonly IMongoCollection<StudyMaterial> _studyMaterials;

        public StudyMaterialRepository(IMongoDatabase database)
        {
            _studyMaterials = database.GetCollection<StudyMaterial>("StudyMaterials");
        }

        public async Task<StudyMaterial> GetByIdAsync(string id)
        {
            return await _studyMaterials.Find(s => s.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<StudyMaterial>> GetAllAsync()
        {
            return await _studyMaterials.Find(_ => true).ToListAsync();
        }

        public async Task<long> CountAsync()
        {
            return await _studyMaterials.CountDocumentsAsync(_ => true);
        }

        public async Task<List<StudyMaterial>> GetByCourseIdAsync(string courseId)
        {
            return await _studyMaterials.Find(s => s.CourseId == courseId).ToListAsync();
        }

        public async Task<List<StudyMaterial>> GetByCourseIdsAsync(List<string> courseIds)
        {
            return await _studyMaterials.Find(s => courseIds.Contains(s.CourseId)).ToListAsync();
        }

        public async Task<StudyMaterial> CreateAsync(StudyMaterial studyMaterial)
        {
            studyMaterial.CreatedAt = DateTime.UtcNow;
            await _studyMaterials.InsertOneAsync(studyMaterial);
            return studyMaterial;
        }

        public async Task UpdateAsync(string id, StudyMaterial studyMaterial)
        {
            studyMaterial.UpdatedAt = DateTime.UtcNow;
            await _studyMaterials.ReplaceOneAsync(s => s.Id == id, studyMaterial);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _studyMaterials.DeleteOneAsync(s => s.Id == id);
            return result.DeletedCount > 0;
        }
    }
}

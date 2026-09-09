using LearningBackendAPI.Models;
using MongoDB.Driver;

namespace LearningBackendAPI.Repositories
{
    public class VideoMaterialRepository : IVideoMaterialRepository
    {
        private readonly IMongoCollection<VideoMaterial> _videoMaterials;

        public VideoMaterialRepository(IMongoDatabase database)
        {
            _videoMaterials = database.GetCollection<VideoMaterial>("VideoMaterials");
        }

        public async Task<VideoMaterial> GetByIdAsync(string id)
        {
            return await _videoMaterials.Find(v => v.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<VideoMaterial>> GetAllAsync()
        {
            return await _videoMaterials.Find(_ => true).ToListAsync();
        }

        public async Task<long> CountAsync()
        {
            return await _videoMaterials.CountDocumentsAsync(_ => true);
        }

        public async Task<List<VideoMaterial>> GetByCourseIdAsync(string courseId)
        {
            return await _videoMaterials.Find(v => v.CourseId == courseId).ToListAsync();
        }

        public async Task<List<VideoMaterial>> GetByCourseIdsAsync(List<string> courseIds)
        {
            return await _videoMaterials.Find(v => courseIds.Contains(v.CourseId)).ToListAsync();
        }

        public async Task<VideoMaterial> CreateAsync(VideoMaterial videoMaterial)
        {
            videoMaterial.CreatedAt = DateTime.UtcNow;
            await _videoMaterials.InsertOneAsync(videoMaterial);
            return videoMaterial;
        }

        public async Task UpdateAsync(string id, VideoMaterial videoMaterial)
        {
            videoMaterial.UpdatedAt = DateTime.UtcNow;
            await _videoMaterials.ReplaceOneAsync(v => v.Id == id, videoMaterial);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _videoMaterials.DeleteOneAsync(v => v.Id == id);
            return result.DeletedCount > 0;
        }
    }
}

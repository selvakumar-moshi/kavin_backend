using LearningBackendAPI.Models;
using MongoDB.Driver;

namespace LearningBackendAPI.Repositories
{
    public class SyllabusMaterialRepository : ISyllabusMaterialRepository
    {
        private readonly IMongoCollection<SyllabusMaterial> _materials;

        public SyllabusMaterialRepository(IMongoDatabase database)
        {
            _materials = database.GetCollection<SyllabusMaterial>("SyllabusMaterials");
        }

        // Newest first
        public async Task<List<SyllabusMaterial>> GetAllAsync()
            => await _materials.Find(_ => true).SortByDescending(m => m.CreatedAt).ToListAsync();

        public async Task<SyllabusMaterial?> GetByIdAsync(string id)
            => await _materials.Find(m => m.Id == id).FirstOrDefaultAsync();

        public async Task<SyllabusMaterial> CreateAsync(SyllabusMaterial material)
        {
            material.CreatedAt = DateTime.UtcNow;
            await _materials.InsertOneAsync(material);
            return material;
        }

        public async Task UpdateAsync(SyllabusMaterial material)
        {
            material.UpdatedAt = DateTime.UtcNow;
            await _materials.ReplaceOneAsync(m => m.Id == material.Id, material);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _materials.DeleteOneAsync(m => m.Id == id);
            return result.DeletedCount > 0;
        }
    }
}

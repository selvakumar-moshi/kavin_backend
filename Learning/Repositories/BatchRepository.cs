using LearningBackendAPI.Models;
using MongoDB.Driver;

namespace LearningBackendAPI.Repositories
{
    public class BatchRepository : IBatchRepository
    {
        private readonly IMongoCollection<Batch> _batches;

        public BatchRepository(IMongoDatabase database)
        {
            _batches = database.GetCollection<Batch>("Batches");
        }

        public async Task<Batch> GetByIdAsync(string id)
        {
            return await _batches.Find(b => b.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<Batch>> GetAllAsync()
        {
            return await _batches.Find(_ => true).ToListAsync();
        }

        public async Task<long> CountAsync()
        {
            return await _batches.CountDocumentsAsync(_ => true);
        }

        public async Task<List<Batch>> GetByCourseIdAsync(string courseId)
        {
            return await _batches.Find(b => b.CourseId == courseId).ToListAsync();
        }

        public async Task<Batch> CreateAsync(Batch batch)
        {
            batch.CreatedAt = DateTime.UtcNow;
            await _batches.InsertOneAsync(batch);
            return batch;
        }

        public async Task UpdateAsync(string id, Batch batch)
        {
            batch.UpdatedAt = DateTime.UtcNow;
            await _batches.ReplaceOneAsync(b => b.Id == id, batch);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _batches.DeleteOneAsync(b => b.Id == id);
            return result.DeletedCount > 0;
        }
    }
}

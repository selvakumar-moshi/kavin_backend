using LearningBackendAPI.Models;
using MongoDB.Driver;

namespace LearningBackendAPI.Repositories
{
    public class LiveClassRepository : ILiveClassRepository
    {
        private readonly IMongoCollection<LiveClass> _liveClasses;

        public LiveClassRepository(IMongoDatabase database)
        {
            _liveClasses = database.GetCollection<LiveClass>("LiveClasses");
        }

        public async Task<LiveClass?> GetByIdAsync(string id)
            => await _liveClasses.Find(l => l.Id == id).FirstOrDefaultAsync();

        public async Task<List<LiveClass>> GetAllAsync()
            => await _liveClasses.Find(_ => true).ToListAsync();

        public async Task<LiveClass> CreateAsync(LiveClass liveClass)
        {
            liveClass.CreatedAt = DateTime.UtcNow;
            await _liveClasses.InsertOneAsync(liveClass);
            return liveClass;
        }

        public async Task UpdateAsync(string id, LiveClass liveClass)
        {
            liveClass.UpdatedAt = DateTime.UtcNow;
            await _liveClasses.ReplaceOneAsync(l => l.Id == id, liveClass);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _liveClasses.DeleteOneAsync(l => l.Id == id);
            return result.DeletedCount > 0;
        }
    }
}

using LearningBackendAPI.Models;
using MongoDB.Driver;

namespace LearningBackendAPI.Repositories
{
    public class QuizStructureRepository : IQuizStructureRepository
    {
        private static readonly Dictionary<Type, string> CollectionNames = new()
        {
            [typeof(QuizSubject)] = "QuizSubjects",
            [typeof(QuizCategory)] = "QuizCategories",
            [typeof(QuizStandard)] = "QuizStandards",
            [typeof(QuizPart)] = "QuizParts",
            [typeof(QuizFolder)] = "QuizFolders",
            [typeof(QuizSubFolder)] = "QuizSubFolders"
        };

        private readonly IMongoDatabase _database;

        public QuizStructureRepository(IMongoDatabase database)
        {
            _database = database;
        }

        private IMongoCollection<T> Collection<T>() where T : QuizStructureItem
            => _database.GetCollection<T>(CollectionNames[typeof(T)]);

        public async Task<List<T>> GetAllAsync<T>() where T : QuizStructureItem
        {
            // ObjectIds ascend with creation, so this is oldest-first
            return await Collection<T>().Find(_ => true).SortBy(x => x.Id).ToListAsync();
        }

        public async Task<T> CreateAsync<T>(T item) where T : QuizStructureItem
        {
            item.CreatedAt = DateTime.UtcNow;
            await Collection<T>().InsertOneAsync(item);
            return item;
        }

        public async Task UpdateAsync<T>(T item) where T : QuizStructureItem
        {
            await Collection<T>().ReplaceOneAsync(x => x.Id == item.Id, item);
        }

        public async Task<bool> DeleteAsync<T>(string id) where T : QuizStructureItem
        {
            var result = await Collection<T>().DeleteOneAsync(x => x.Id == id);
            return result.DeletedCount > 0;
        }
    }
}

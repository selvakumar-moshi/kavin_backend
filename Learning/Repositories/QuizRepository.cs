using LearningBackendAPI.Models;
using MongoDB.Driver;

namespace LearningBackendAPI.Repositories
{
    public class QuizRepository : IQuizRepository
    {
        private readonly IMongoCollection<Quiz> _quizzes;

        public QuizRepository(IMongoDatabase database)
        {
            _quizzes = database.GetCollection<Quiz>("Quizzes");
        }

        public async Task<Quiz> GetByIdAsync(string id)
        {
            return await _quizzes.Find(q => q.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<Quiz>> GetAllAsync()
        {
            return await _quizzes.Find(_ => true).ToListAsync();
        }

        public async Task<long> CountTotalQuestionsAsync()
        {
            var questionCounts = await _quizzes.Find(_ => true)
                .Project(q => q.Questions.Count)
                .ToListAsync();

            return questionCounts.Sum(count => (long)count);
        }

        public async Task<List<Quiz>> GetByCourseIdsAsync(List<string> courseIds)
        {
            return await _quizzes.Find(q => courseIds.Contains(q.CourseId)).ToListAsync();
        }

        public async Task<Quiz> CreateAsync(Quiz quiz)
        {
            quiz.CreatedAt = DateTime.UtcNow;
            await _quizzes.InsertOneAsync(quiz);
            return quiz;
        }

        public async Task UpdateAsync(string id, Quiz quiz)
        {
            quiz.UpdatedAt = DateTime.UtcNow;
            await _quizzes.ReplaceOneAsync(q => q.Id == id, quiz);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _quizzes.DeleteOneAsync(q => q.Id == id);
            return result.DeletedCount > 0;
        }
    }
}

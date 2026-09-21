using LearningBackendAPI.Models;
using MongoDB.Driver;

namespace LearningBackendAPI.Repositories
{
    public class QuizAttemptRepository : IQuizAttemptRepository
    {
        private readonly IMongoCollection<QuizAttempt> _attempts;

        public QuizAttemptRepository(IMongoDatabase database)
        {
            _attempts = database.GetCollection<QuizAttempt>("QuizAttempts");
        }

        public async Task<QuizAttempt?> GetByQuizAndUserAsync(string quizId, string userId)
        {
            return await _attempts.Find(a => a.QuizId == quizId && a.UserId == userId)
                .SortByDescending(a => a.SubmittedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<QuizAttempt?> GetByQuizUserAndVersionAsync(string quizId, string userId, int quizVersion)
        {
            return await _attempts
                .Find(a => a.QuizId == quizId && a.UserId == userId && a.QuizVersion == quizVersion)
                .FirstOrDefaultAsync();
        }

        public async Task<List<QuizAttempt>> GetByQuizIdAsync(string quizId)
        {
            return await _attempts.Find(a => a.QuizId == quizId).ToListAsync();
        }

        public async Task<QuizAttempt> CreateAsync(QuizAttempt attempt)
        {
            attempt.SubmittedAt = DateTime.UtcNow;
            await _attempts.InsertOneAsync(attempt);
            return attempt;
        }
    }
}

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

        // The student's best attempt on the quiz's latest version (ties go to the earlier submission, like the rank list)
        public async Task<QuizAttempt?> GetByQuizAndUserAsync(string quizId, string userId)
        {
            return await _attempts.Find(a => a.QuizId == quizId && a.UserId == userId)
                .SortByDescending(a => a.QuizVersion)
                .ThenByDescending(a => a.ScoredMarks)
                .ThenBy(a => a.SubmittedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<long> CountByQuizUserAndVersionAsync(string quizId, string userId, int quizVersion)
        {
            return await _attempts.CountDocumentsAsync(a => a.QuizId == quizId && a.UserId == userId && a.QuizVersion == quizVersion);
        }

        public async Task<List<(string QuizId, int QuizVersion)>> GetAttemptKeysByUserAsync(string userId)
        {
            var keys = await _attempts.Find(a => a.UserId == userId)
                .Project(a => new { a.QuizId, a.QuizVersion })
                .ToListAsync();
            return keys.Select(k => (k.QuizId, k.QuizVersion)).ToList();
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

using LearningBackendAPI.Models;

namespace LearningBackendAPI.Repositories
{
    public interface IQuizAttemptRepository
    {
        Task<QuizAttempt?> GetByQuizAndUserAsync(string quizId, string userId);
        Task<long> CountByQuizUserAndVersionAsync(string quizId, string userId, int quizVersion);
        // (QuizId, QuizVersion) of every attempt the user has made - used to work out attempts left per quiz
        Task<List<(string QuizId, int QuizVersion)>> GetAttemptKeysByUserAsync(string userId);
        Task<List<QuizAttempt>> GetByQuizIdAsync(string quizId);
        Task<QuizAttempt> CreateAsync(QuizAttempt attempt);
    }
}

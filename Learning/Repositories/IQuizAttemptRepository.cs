using LearningBackendAPI.Models;

namespace LearningBackendAPI.Repositories
{
    public interface IQuizAttemptRepository
    {
        Task<QuizAttempt?> GetByQuizAndUserAsync(string quizId, string userId);
        Task<QuizAttempt?> GetByQuizUserAndVersionAsync(string quizId, string userId, int quizVersion);
        Task<List<QuizAttempt>> GetByQuizIdAsync(string quizId);
        Task<QuizAttempt> CreateAsync(QuizAttempt attempt);
    }
}

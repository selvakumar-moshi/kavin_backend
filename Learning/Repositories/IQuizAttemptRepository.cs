using LearningBackendAPI.Models;

namespace LearningBackendAPI.Repositories
{
    public interface IQuizAttemptRepository
    {
        Task<QuizAttempt?> GetByQuizAndUserAsync(string quizId, string userId);
        Task<List<QuizAttempt>> GetByQuizIdAsync(string quizId);
        Task<QuizAttempt> CreateAsync(QuizAttempt attempt);
    }
}

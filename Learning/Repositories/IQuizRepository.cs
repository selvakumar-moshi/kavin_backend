using LearningBackendAPI.Models;

namespace LearningBackendAPI.Repositories
{
    public interface IQuizRepository
    {
        Task<Quiz> GetByIdAsync(string id);
        Task<List<Quiz>> GetAllAsync();
        Task<long> CountTotalQuestionsAsync();
        Task<List<Quiz>> GetByCourseIdsAsync(List<string> courseIds);
        Task<Quiz> CreateAsync(Quiz quiz);
        Task UpdateAsync(string id, Quiz quiz);
        Task<bool> DeleteAsync(string id);
    }
}

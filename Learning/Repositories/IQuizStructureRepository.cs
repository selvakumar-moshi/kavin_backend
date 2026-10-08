using LearningBackendAPI.Models;

namespace LearningBackendAPI.Repositories
{
    // One generic repository over the four quiz-structure collections (subjects, categories, standards, parts)
    public interface IQuizStructureRepository
    {
        Task<List<T>> GetAllAsync<T>() where T : QuizStructureItem;
        Task<T> CreateAsync<T>(T item) where T : QuizStructureItem;
        Task UpdateAsync<T>(T item) where T : QuizStructureItem;
        Task<bool> DeleteAsync<T>(string id) where T : QuizStructureItem;
    }
}

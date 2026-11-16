using LearningBackendAPI.Models;

namespace LearningBackendAPI.Repositories
{
    public interface IQuestionPaperRepository
    {
        Task<QuestionPaper> GetByIdAsync(string id);
        Task<List<QuestionPaper>> GetAllAsync();
        Task<long> CountAsync();
        Task<List<QuestionPaper>> GetByCourseIdAsync(string courseId);
        Task<List<QuestionPaper>> GetByCourseIdsAsync(List<string> courseIds);
        Task<QuestionPaper> CreateAsync(QuestionPaper questionPaper);
        Task UpdateAsync(string id, QuestionPaper questionPaper);
        Task<bool> DeleteAsync(string id);
    }
}

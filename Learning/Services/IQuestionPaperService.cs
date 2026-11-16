using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;

namespace LearningBackendAPI.Services
{
    public interface IQuestionPaperService
    {
        Task<QuestionPaper> CreateQuestionPaperAsync(QuestionPaperRequest request);
        Task<PagedResult<QuestionPaper>> GetAccessibleQuestionPapersAsync(string userId, string role, string? courseId, string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize);
        Task<QuestionPaper> GetQuestionPaperByIdAsync(string id, string userId, string role);
        Task<QuestionPaper> UpdateQuestionPaperAsync(string id, QuestionPaperRequest request);
        Task<bool> DeleteQuestionPaperAsync(string id);
    }
}

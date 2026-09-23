using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;

namespace LearningBackendAPI.Services
{
    public interface IQuizService
    {
        Task<Quiz> CreateQuizAsync(QuizCreateRequest request);
        Task<Quiz> UpdateQuizAsync(string id, QuizUpdateRequest request);
        Task<Quiz> PublishQuizAsync(string id, DateTime expiresAt);
        Task<bool> DeleteQuizAsync(string id);
        Task<PagedResult<Quiz>> GetAllQuizzesForAdminAsync(string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize);
        Task<Quiz> GetQuizByIdForAdminAsync(string id);

        Task<PagedResult<QuizStudentResponse>> GetAccessibleQuizzesForStudentAsync(string userId, string? courseId, string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize);
        Task<QuizStudentResponse> GetQuizByIdForStudentAsync(string id, string userId);

        Task<QuizResultResponse> SubmitQuizAsync(string quizId, string userId, QuizSubmitRequest request);
        Task<QuizResultResponse> GetMyResultAsync(string quizId, string userId);
        Task<List<RankListEntryDto>> GetRankListAsync(string quizId, string userId, bool isAdmin);
        Task<(byte[] Content, string FileName)> ExportRankListAsync(string quizId, string userId, bool isAdmin);
    }
}

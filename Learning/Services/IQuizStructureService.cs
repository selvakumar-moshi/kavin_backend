using LearningBackendAPI.DTOs;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public interface IQuizStructureService
    {
        Task<List<QuizSubjectResponse>> GetSubjectsAsync();
        Task<QuizSubjectResponse> CreateSubjectAsync(QuizSubjectRequest request);
        Task<QuizSubjectResponse> UpdateSubjectAsync(string id, QuizSubjectRequest request);
        Task DeleteSubjectAsync(string id);

        Task<List<QuizCategoryResponse>> GetCategoriesAsync(string? subject);
        Task<QuizCategoryResponse> CreateCategoryAsync(QuizCategoryRequest request);
        Task<QuizCategoryResponse> UpdateCategoryAsync(string id, QuizCategoryRequest request);
        Task DeleteCategoryAsync(string id);

        Task<List<QuizStandardResponse>> GetStandardsAsync();
        Task<QuizStandardResponse> CreateStandardAsync(QuizStandardRequest request);
        Task<QuizStandardResponse> UpdateStandardAsync(string id, QuizStandardRequest request);
        Task DeleteStandardAsync(string id);

        Task<List<QuizPartResponse>> GetPartsAsync();
        Task<QuizPartResponse> CreatePartAsync(QuizPartRequest request);
        Task<QuizPartResponse> UpdatePartAsync(string id, QuizPartRequest request);
        Task DeletePartAsync(string id);

        /// <summary>The Subject → (Category) → Standard → (Part) tree for the UI's cascading dropdowns.</summary>
        Task<object> GetTreeAsync();

        /// <summary>
        /// Checks a Subject/Category/Standard/Part selection against the tree and returns it in its
        /// canonical spelling. Sending nothing at all is allowed (the quiz is just not classified).
        /// </summary>
        Task<QuizClassification> ValidateAsync(string? subject, string? category, int? standard, string? part);

        /// <summary>Loads the original Tamil / GK tree the first time, when nothing has been created yet.</summary>
        Task SeedDefaultsAsync();
    }
}

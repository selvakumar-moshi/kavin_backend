using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;

namespace LearningBackendAPI.Services
{
    public interface ISyllabusMaterialService
    {
        Task<SyllabusMaterial> CreateAsync(SyllabusMaterialRequest request);
        /// <summary>Admin: any material. Student: only one their verified syllabus wise payment covers.</summary>
        Task<SyllabusMaterial> GetByIdAsync(string id, string userId, bool isAdmin);
        Task<PagedResult<SyllabusMaterial>> SearchAsync(string userId, bool isAdmin, SyllabusMaterialSearchRequest? request);
        Task<SyllabusMaterial> UpdateAsync(string id, SyllabusMaterialRequest request);
        Task DeleteAsync(string id);
        /// <summary>Every material the student has paid for (used in the user details response).</summary>
        Task<List<SyllabusMaterial>> GetPurchasedAsync(string userId);
    }
}

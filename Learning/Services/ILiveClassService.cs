using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;

namespace LearningBackendAPI.Services
{
    public interface ILiveClassService
    {
        Task<LiveClass> CreateAsync(LiveClassRequest request);
        /// <summary>Admin: every live class. Student: no Expired ones, and Paid ones only if enrolled in that course/batch.</summary>
        Task<List<LiveClass>> GetAllAsync(bool isAdmin, string userId);
        /// <summary>For a student, an Expired live class or a Paid one they have no access to is "not found".</summary>
        Task<LiveClass> GetByIdAsync(string id, bool isAdmin, string userId);
        Task<LiveClass> UpdateAsync(string id, LiveClassRequest request);
        Task<bool> DeleteAsync(string id);
    }
}

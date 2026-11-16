using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;

namespace LearningBackendAPI.Services
{
    public interface ILiveClassService
    {
        Task<LiveClass> CreateAsync(LiveClassRequest request);
        /// <summary>Admin: every live class. Student: everything except Expired ones.</summary>
        Task<List<LiveClass>> GetAllAsync(bool isAdmin);
        /// <summary>An Expired live class is only returned to an admin (a student gets "not found").</summary>
        Task<LiveClass> GetByIdAsync(string id, bool isAdmin);
        Task<LiveClass> UpdateAsync(string id, LiveClassRequest request);
        Task<bool> DeleteAsync(string id);
    }
}

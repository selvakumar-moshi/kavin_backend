using LearningBackendAPI.Models;

namespace LearningBackendAPI.Services
{
    public interface IEnrollmentService
    {
        Task<List<Enrollment>> GetAllEnrollmentsAsync();
        Task<List<Enrollment>> GetEnrollmentsByUserIdAsync(string userId);
        Task<Enrollment> GetEnrollmentByIdAsync(string id);
        Task<Enrollment> UpdateStatusAsync(string id, string status, string? paymentMethod, string? transactionReference, string adminUserId);
    }
}

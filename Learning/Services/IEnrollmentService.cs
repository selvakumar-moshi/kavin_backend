using LearningBackendAPI.Models;

namespace LearningBackendAPI.Services
{
    public interface IEnrollmentService
    {
        Task<List<Enrollment>> GetAllEnrollmentsAsync();
        Task<List<Enrollment>> GetEnrollmentsByUserIdAsync(string userId);
        Task<Enrollment> GetEnrollmentByIdAsync(string id);
        Task<Enrollment> UpdateStatusAsync(string id, string status, string? paymentMethod, string? transactionReference, string adminUserId);
        Task<Enrollment> EnrollAsync(string userId, string? courseId, string? batchId);
        Task<Enrollment> UploadPaymentAttachmentAsync(string id, string userId, IFormFile file);
        Task<(byte[] Content, string FileName)> ExportCourseEnrollmentsAsync(string courseId);
    }
}

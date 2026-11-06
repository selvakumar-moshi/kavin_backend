using LearningBackendAPI.Models;

namespace LearningBackendAPI.Repositories
{
    public interface ISyllabusWiseRepository
    {
        Task<List<SyllabusWisePlan>> GetPlansAsync();
        Task<SyllabusWisePlan?> GetPlanByIdAsync(string id);
        Task<SyllabusWisePlan> CreatePlanAsync(SyllabusWisePlan plan);
        Task UpdatePlanAsync(SyllabusWisePlan plan);
        Task<bool> DeletePlanAsync(string id);

        Task<List<SyllabusWisePayment>> GetPaymentsAsync(string? userId = null);
        Task<SyllabusWisePayment?> GetPaymentByIdAsync(string id);
        Task<SyllabusWisePayment?> GetPaymentAsync(string userId, string planId);
        Task<SyllabusWisePayment> CreatePaymentAsync(SyllabusWisePayment payment);
        Task UpdatePaymentAsync(SyllabusWisePayment payment);
        Task<List<SyllabusWisePayment>> GetVerifiedPaymentsAsync(string userId);
    }
}

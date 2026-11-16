using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;

namespace LearningBackendAPI.Services
{
    public interface ISyllabusWiseService
    {
        Task<List<SyllabusWisePlan>> GetPlansAsync();
        Task<SyllabusWisePlan> GetPlanAsync(string id);
        Task<SyllabusWisePlan> CreatePlanAsync(SyllabusWisePlanRequest request);
        Task<SyllabusWisePlan> UpdatePlanAsync(string id, SyllabusWisePlanRequest request);
        Task DeletePlanAsync(string id);

        /// <summary>The signed-in student uploads a payment screenshot: Pending until the admin verifies or rejects it.</summary>
        Task<SyllabusWisePaymentDto> SubmitPaymentAsync(string userId, SyllabusWisePaymentRequest request);
        /// <summary>The folders (with everything nested below) the student's verified payments unlock.</summary>
        Task<SyllabusAccess> GetAccessAsync(string userId);
        Task<List<SyllabusWisePaymentDto>> GetPaymentsAsync(string? userId);
        Task<SyllabusWisePaymentDto> UpdatePaymentStatusAsync(string id, SyllabusWisePaymentStatusRequest request, string? adminUserId = null);
    }
}

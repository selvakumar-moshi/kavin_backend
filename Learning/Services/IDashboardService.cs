using LearningBackendAPI.DTOs;

namespace LearningBackendAPI.Services
{
    public interface IDashboardService
    {
        Task<DashboardCountsResponse> GetCountsAsync();
    }
}

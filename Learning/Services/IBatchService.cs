using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;

namespace LearningBackendAPI.Services
{
    public interface IBatchService
    {
        Task<Batch> CreateBatchAsync(BatchCreateRequest request);
        Task<Batch> UpdateBatchAsync(string id, BatchUpdateRequest request);
        Task<PagedResult<Batch>> GetBatchesAsync(string? courseId, bool includeExpired, string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize);
        Task<bool> DeleteBatchAsync(string id);
    }
}

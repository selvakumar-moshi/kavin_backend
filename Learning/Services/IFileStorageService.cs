namespace LearningBackendAPI.Services
{
    public interface IFileStorageService
    {
        Task<(string Url, string Key)> UploadPdfAsync(IFormFile file, string folder);
        Task<(string Url, string Key)> UploadImageAsync(IFormFile file, string folder, string? fileName = null);
        Task DeleteAsync(string key);
    }
}

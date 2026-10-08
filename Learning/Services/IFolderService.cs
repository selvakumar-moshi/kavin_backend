using LearningBackendAPI.DTOs;

namespace LearningBackendAPI.Services
{
    public interface IFolderService
    {
        /// <summary>Every folder with its sub folders.</summary>
        Task<List<FolderResponse>> GetFoldersAsync();
        Task<FolderResponse> CreateFolderAsync(FolderRequest request);
        Task<FolderResponse> UpdateFolderAsync(string id, FolderRequest request);
        Task DeleteFolderAsync(string id);

        Task<List<SubFolderResponse>> GetSubFoldersAsync(string? folderId);
        Task<SubFolderResponse> CreateSubFolderAsync(SubFolderRequest request);
        Task<SubFolderResponse> UpdateSubFolderAsync(string id, SubFolderRequest request);
        Task DeleteSubFolderAsync(string id);

        /// <summary>
        /// Checks a quiz's folder / sub folder choice and returns it with the names. Nothing passed is allowed
        /// (empty selection). A sub folder is required when the folder has any, and must belong to the folder.
        /// </summary>
        Task<FolderSelection> ValidateAsync(string? folderId, string? subFolderId);
    }
}

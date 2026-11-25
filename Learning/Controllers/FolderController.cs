using LearningBackendAPI.DTOs;
using LearningBackendAPI.Helpers;
using LearningBackendAPI.Services;
using LearningBackendAPI.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearningBackendAPI.Controllers
{
    /// <summary>
    /// Folders for "previousYear", "syllabusWise" and "school" quizzes. Reading is open to any signed-in user; creating, updating and
    /// deleting is Admin only. A folder with sub folders, or one that quizzes use, can't be deleted.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FolderController : BusinessControllerBase
    {
        private readonly IFolderService _folderService;

        public FolderController(IFolderService folderService, ResponseHelper responseHelper) : base(responseHelper)
        {
            _folderService = folderService;
        }

        /// <summary>Every folder, each with its sub folders - for the folder / sub folder dropdowns.</summary>
        [HttpGet]
        public Task<IActionResult> GetAll() => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _folderService.GetFoldersAsync(), "Folders retrieved successfully")));

        /// <summary>
        /// Form data: name, subFolders? (JSON array string - each node a name or { name, subFolders? }, nested to any depth,
        /// created with the folder in one call), file? (optional picture, JPG/PNG/WEBP max 5 MB, returned as imageUrl).
        /// </summary>
        [HttpPost]
        [Authorize(Roles = Constants.Roles.Admin)]
        [Consumes("multipart/form-data")]
        public Task<IActionResult> Create([FromForm] FolderFormRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _folderService.CreateFolderAsync(request.ToRequest()), "Folder created successfully")));

        /// <summary>Form data: name?, subFolders?, file?, removeImage?. name renames it (quizzes in the folder pick up the new name); subFolders is a
        /// nested tree added under it - names that already exist are reused, new ones are created.
        /// file replaces the picture, removeImage=true removes it.</summary>
        [HttpPut("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        [Consumes("multipart/form-data")]
        public Task<IActionResult> Update(string id, [FromForm] FolderFormRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _folderService.UpdateFolderAsync(id, request.ToRequest()), "Folder updated successfully")));

        [HttpDelete("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> Delete(string id) => HandleAsync(async () =>
        {
            await _folderService.DeleteFolderAsync(id);
            return Ok(_responseHelper.Success<object?>(null, "Folder deleted successfully"));
        });
    }
}

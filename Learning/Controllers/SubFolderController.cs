using LearningBackendAPI.DTOs;
using LearningBackendAPI.Helpers;
using LearningBackendAPI.Services;
using LearningBackendAPI.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearningBackendAPI.Controllers
{
    /// <summary>
    /// Sub folders inside a folder or inside another sub folder, to any depth (see /api/Folder). Reading is open to any signed-in user; creating,
    /// updating and deleting is Admin only. A sub folder that quizzes use can't be deleted.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SubFolderController : BusinessControllerBase
    {
        private readonly IFolderService _folderService;

        public SubFolderController(IFolderService folderService, ResponseHelper responseHelper) : base(responseHelper)
        {
            _folderService = folderService;
        }

        /// <summary>Optional ?folderId= to list one folder's sub folders (GET /api/Folder already nests them).</summary>
        [HttpGet]
        public Task<IActionResult> GetAll([FromQuery] string? folderId) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _folderService.GetSubFoldersAsync(folderId), "Sub folders retrieved successfully")));

        /// <summary>
        /// Form data: folderId (a folder or another sub folder), name, subFolders? (JSON array string, a nested branch created
        /// under the new sub folder in the same call), file? (optional picture, JPG/PNG/WEBP max 5 MB, returned as imageUrl).
        /// </summary>
        [HttpPost]
        [Authorize(Roles = Constants.Roles.Admin)]
        [Consumes("multipart/form-data")]
        public Task<IActionResult> Create([FromForm] SubFolderFormRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _folderService.CreateSubFolderAsync(request.ToRequest()), "Sub folder created successfully")));

        /// <summary>Form data: name?, subFolders?, file?, removeImage?. name renames it (quizzes in it pick up the new name); subFolders is a nested
        /// tree added under it - names that already exist are reused, new ones are created. Its parent can't be changed.
        /// file replaces the picture, removeImage=true removes it.</summary>
        [HttpPut("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        [Consumes("multipart/form-data")]
        public Task<IActionResult> Update(string id, [FromForm] SubFolderFormRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _folderService.UpdateSubFolderAsync(id, request.ToRequest()), "Sub folder updated successfully")));

        [HttpDelete("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> Delete(string id) => HandleAsync(async () =>
        {
            await _folderService.DeleteSubFolderAsync(id);
            return Ok(_responseHelper.Success<object?>(null, "Sub folder deleted successfully"));
        });
    }
}

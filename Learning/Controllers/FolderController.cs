using LearningBackendAPI.DTOs;
using LearningBackendAPI.Helpers;
using LearningBackendAPI.Services;
using LearningBackendAPI.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearningBackendAPI.Controllers
{
    /// <summary>
    /// Folders for "previousYear" quizzes. Reading is open to any signed-in user; creating, updating and
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

        /// <summary>Body: { name }.</summary>
        [HttpPost]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> Create([FromBody] FolderRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _folderService.CreateFolderAsync(request), "Folder created successfully")));

        /// <summary>Body: { name }. Quizzes in the folder pick up the new name.</summary>
        [HttpPut("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> Update(string id, [FromBody] FolderRequest request) => HandleAsync(async () =>
            Ok(_responseHelper.Success(await _folderService.UpdateFolderAsync(id, request), "Folder updated successfully")));

        [HttpDelete("{id}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        public Task<IActionResult> Delete(string id) => HandleAsync(async () =>
        {
            await _folderService.DeleteFolderAsync(id);
            return Ok(_responseHelper.Success<object?>(null, "Folder deleted successfully"));
        });
    }
}

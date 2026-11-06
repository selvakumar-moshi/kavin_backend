using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public class SyllabusMaterialService : ISyllabusMaterialService
    {
        private const long MaxPdfSizeInBytes = 10 * 1024 * 1024; // 10 MB
        private const string PdfFolder = "coaching/syllabus/material";

        private readonly ISyllabusMaterialRepository _repository;
        private readonly IFolderService _folderService;
        private readonly ISyllabusWiseService _syllabusWiseService;
        private readonly IFileStorageService _fileStorageService;

        public SyllabusMaterialService(
            ISyllabusMaterialRepository repository,
            IFolderService folderService,
            ISyllabusWiseService syllabusWiseService,
            IFileStorageService fileStorageService)
        {
            _repository = repository;
            _folderService = folderService;
            _syllabusWiseService = syllabusWiseService;
            _fileStorageService = fileStorageService;
        }

        public async Task<SyllabusMaterial> CreateAsync(SyllabusMaterialRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                throw new InvalidOperationException("Title is required");
            }
            if (string.IsNullOrWhiteSpace(request.FolderId))
            {
                throw new InvalidOperationException("Folder is required");
            }
            if (request.File == null || request.File.Length == 0)
            {
                throw new InvalidOperationException("PDF file is required");
            }
            ValidatePdf(request.File);

            var folder = await _folderService.ValidateAsync(request.FolderId, request.SubFolderId);
            var (name, url, key) = await UploadPdfAsync(request.File);

            var material = await _repository.CreateAsync(new SyllabusMaterial
            {
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                FolderId = folder.FolderId!,
                SubFolderId = folder.SubFolderId,
                PdfUrl = url,
                PdfKey = key,
                PdfFileName = name
            });
            await FillDetailsAsync(new List<SyllabusMaterial> { material });
            return material;
        }

        public async Task<SyllabusMaterial> GetByIdAsync(string id, string userId, bool isAdmin)
        {
            var material = await _repository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Syllabus material not found");

            if (!isAdmin && !(await _syllabusWiseService.GetAccessAsync(userId)).Covers(material.FolderId, material.SubFolderId))
            {
                throw new UnauthorizedAccessException("Buy the syllabus wise plan to open this material");
            }

            await FillDetailsAsync(new List<SyllabusMaterial> { material });
            return material;
        }

        private static readonly Dictionary<string, Func<SyllabusMaterial, string?>> SearchFields = new()
        {
            ["title"] = m => m.Title,
            ["description"] = m => m.Description,
            ["pdffilename"] = m => m.PdfFileName
        };
        private static readonly string[] DefaultSearchFields = { "title", "description" };

        public async Task<PagedResult<SyllabusMaterial>> SearchAsync(string userId, bool isAdmin, SyllabusMaterialSearchRequest? request)
        {
            var materials = await _repository.GetAllAsync();

            if (!isAdmin)
            {
                var access = await _syllabusWiseService.GetAccessAsync(userId);
                materials = materials.Where(m => access.Covers(m.FolderId, m.SubFolderId)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(request?.FolderId))
            {
                var folderId = request.FolderId.Trim();
                materials = materials.Where(m => m.FolderId == folderId).ToList();
            }
            if (!string.IsNullOrWhiteSpace(request?.SubFolderId))
            {
                // A sub folder also matches everything filed below it
                var subtree = await _folderService.GetSubtreeIdsAsync(request.SubFolderId.Trim());
                materials = materials.Where(m => m.SubFolderId != null && subtree.Contains(m.SubFolderId)).ToList();
            }

            var filtered = TextSearchHelper.ApplyFilter(materials, request?.SearchTerm, null, SearchFields, DefaultSearchFields);
            var page = PagingHelper.ToPagedResult(filtered, request?.PageNumber ?? 1, request?.PageSize ?? 10);
            await FillDetailsAsync(page.Items);
            return page;
        }

        public async Task<SyllabusMaterial> UpdateAsync(string id, SyllabusMaterialRequest request)
        {
            var material = await _repository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Syllabus material not found");

            if (request.Title != null)
            {
                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    throw new InvalidOperationException("Title can't be empty");
                }
                material.Title = request.Title.Trim();
            }
            if (request.Description != null)
            {
                material.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            }

            // Moving it: a new folder comes with its own sub folder; a sub folder alone stays inside the current folder
            if (!string.IsNullOrWhiteSpace(request.FolderId) || !string.IsNullOrWhiteSpace(request.SubFolderId))
            {
                var folder = await _folderService.ValidateAsync(
                    string.IsNullOrWhiteSpace(request.FolderId) ? material.FolderId : request.FolderId,
                    request.SubFolderId);
                material.FolderId = folder.FolderId!;
                material.SubFolderId = folder.SubFolderId;
            }

            string? oldKey = null;
            if (request.File != null && request.File.Length > 0)
            {
                ValidatePdf(request.File);
                var (name, url, key) = await UploadPdfAsync(request.File);
                oldKey = material.PdfKey;
                material.PdfFileName = name;
                material.PdfUrl = url;
                material.PdfKey = key;
            }

            await _repository.UpdateAsync(material);
            // The old file goes only after the update is saved, so a failed update never loses the file it still points to
            if (!string.IsNullOrWhiteSpace(oldKey))
            {
                await _fileStorageService.DeleteAsync(oldKey);
            }

            await FillDetailsAsync(new List<SyllabusMaterial> { material });
            return material;
        }

        public async Task DeleteAsync(string id)
        {
            var material = await _repository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Syllabus material not found");
            if (await _repository.DeleteAsync(id))
            {
                await _fileStorageService.DeleteAsync(material.PdfKey);
            }
        }

        public async Task<List<SyllabusMaterial>> GetPurchasedAsync(string userId)
        {
            var access = await _syllabusWiseService.GetAccessAsync(userId);
            var materials = (await _repository.GetAllAsync()).Where(m => access.Covers(m.FolderId, m.SubFolderId)).ToList();
            await FillDetailsAsync(materials);
            return materials;
        }

        // Folder / sub folder names and the full sub folder path, looked up from the folder tree
        private async Task FillDetailsAsync(List<SyllabusMaterial> materials)
        {
            if (materials.Count == 0)
            {
                return;
            }

            var names = await _folderService.GetNodeNamesAsync(materials.SelectMany(m => new[] { m.FolderId, m.SubFolderId }));
            var paths = await _folderService.GetSubFolderPathsAsync(materials.Select(m => m.SubFolderId));
            foreach (var material in materials)
            {
                material.FolderName = names.GetValueOrDefault(material.FolderId);
                material.SubFolderName = material.SubFolderId != null ? names.GetValueOrDefault(material.SubFolderId) : null;
                material.SubFolderPath = material.SubFolderId != null ? paths.GetValueOrDefault(material.SubFolderId) : null;
            }
        }

        // The stored name gets a unique prefix so two files called "notes.pdf" never overwrite each other
        private async Task<(string Name, string Url, string Key)> UploadPdfAsync(IFormFile file)
        {
            var name = Path.GetFileName(file.FileName);
            var (url, key) = await _fileStorageService.UploadPdfAsync(file, PdfFolder, $"{name}");
            return (name, url, key);
        }

        private static void ValidatePdf(IFormFile file)
        {
            if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only PDF files are allowed");
            }
            if (file.Length > MaxPdfSizeInBytes)
            {
                throw new InvalidOperationException("PDF file size must not exceed 10 MB");
            }
        }
    }
}

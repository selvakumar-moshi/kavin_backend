using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public class BatchService : IBatchService
    {
        private readonly IBatchRepository _batchRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly IFileStorageService _fileStorageService;

        private const string PdfFolder = "coaching/Batch";
        private const long MaxPdfSizeInBytes = 10 * 1024 * 1024; // 10 MB

        public BatchService(IBatchRepository batchRepository, ICourseRepository courseRepository, IFileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService;
            _batchRepository = batchRepository;
            _courseRepository = courseRepository;
        }

        private static bool HasFile(IFormFile? file) => file != null && file.Length > 0;

        private static void ValidatePdf(IFormFile? file)
        {
            if (file == null)
            {
                return;
            }
            if (file.Length == 0)
            {
                throw new InvalidOperationException("The PDF file is empty");
            }
            if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only PDF files are allowed");
            }
            if (file.Length > MaxPdfSizeInBytes)
            {
                throw new InvalidOperationException("PDF file size must not exceed 10 MB");
            }
        }

        // Each upload gets its own folder, so the URL ends with the file's own name and two batches can't overwrite each other
        private async Task AttachPdfAsync(Batch batch, IFormFile file)
        {
            var name = Path.GetFileName(file.FileName);
            var (url, key) = await _fileStorageService.UploadPdfAsync(file, $"{PdfFolder}/{Guid.NewGuid():N}", name);
            batch.PdfUrl = url;
            batch.PdfName = name;
            batch.PdfKey = key;
        }

        // A file that can't be deleted from S3 must not block the batch change - it is only an orphan
        private async Task DeletePdfAsync(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            try { await _fileStorageService.DeleteAsync(key); } catch { }
        }

        public async Task<Batch> CreateBatchAsync(BatchCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                throw new InvalidOperationException("Title is required");
            }

            if (string.IsNullOrWhiteSpace(request.CourseId))
            {
                throw new InvalidOperationException("Course is required");
            }

            var course = await _courseRepository.GetByIdAsync(request.CourseId);
            if (course == null)
            {
                throw new InvalidOperationException(Constants.Messages.CourseNotFound);
            }

            if (request.BatchFrom == null || request.BatchTo == null)
            {
                throw new InvalidOperationException("Batch start and end dates are required");
            }

            if (request.BatchFrom.Value.Date > request.BatchTo.Value.Date)
            {
                throw new InvalidOperationException(Constants.Messages.BatchDateRangeInvalid);
            }

            ValidatePdf(request.File);

            var batch = new Batch
            {
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                CourseId = course.Id,
                CourseName = course.CourseName,
                BatchFrom = request.BatchFrom.Value.Date,
                BatchTo = request.BatchTo.Value.Date,
                WhatsAppLink = NormalizeLink(request.WhatsAppLink),
                TelegramLink = NormalizeLink(request.TelegramLink),
                CreatedAt = DateTime.UtcNow
            };

            if (HasFile(request.File))
            {
                await AttachPdfAsync(batch, request.File!);
            }

            try
            {
                return await _batchRepository.CreateAsync(batch);
            }
            catch
            {
                await DeletePdfAsync(batch.PdfKey);
                throw;
            }
        }

        public async Task<Batch> UpdateBatchAsync(string id, BatchUpdateRequest request)
        {
            var batch = await _batchRepository.GetByIdAsync(id);
            if (batch == null)
            {
                throw new KeyNotFoundException(Constants.Messages.BatchNotFound);
            }

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                batch.Title = request.Title.Trim();
            }

            if (request.Description != null)
            {
                batch.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.CourseId))
            {
                var course = await _courseRepository.GetByIdAsync(request.CourseId);
                if (course == null)
                {
                    throw new InvalidOperationException(Constants.Messages.CourseNotFound);
                }
                batch.CourseId = course.Id;
                batch.CourseName = course.CourseName;
            }

            if (request.BatchFrom.HasValue)
            {
                batch.BatchFrom = request.BatchFrom.Value.Date;
            }

            if (request.BatchTo.HasValue)
            {
                batch.BatchTo = request.BatchTo.Value.Date;
            }

            if (batch.BatchFrom.Date > batch.BatchTo.Date)
            {
                throw new InvalidOperationException(Constants.Messages.BatchDateRangeInvalid);
            }

            // null = leave unchanged; empty string = clear the link
            if (request.WhatsAppLink != null)
            {
                batch.WhatsAppLink = NormalizeLink(request.WhatsAppLink);
            }

            if (request.TelegramLink != null)
            {
                batch.TelegramLink = NormalizeLink(request.TelegramLink);
            }

            ValidatePdf(request.File);

            // The old PDF is only removed once the batch is saved with its replacement
            string? oldKey = null;
            if (HasFile(request.File))
            {
                oldKey = batch.PdfKey;
                await AttachPdfAsync(batch, request.File!);
            }
            else if (request.RemoveFile == true && batch.PdfKey != null)
            {
                oldKey = batch.PdfKey;
                batch.PdfUrl = null;
                batch.PdfName = null;
                batch.PdfKey = null;
            }

            await _batchRepository.UpdateAsync(id, batch);
            await DeletePdfAsync(oldKey);
            return batch;
        }

        private static string? NormalizeLink(string? link)
        {
            return string.IsNullOrWhiteSpace(link) ? null : link.Trim();
        }

        private static readonly Dictionary<string, Func<Batch, string?>> SearchFields = new()
        {
            ["title"] = b => b.Title,
            ["coursename"] = b => b.CourseName
        };
        private static readonly string[] DefaultSearchFields = { "title", "courseName" };

        public async Task<PagedResult<Batch>> GetBatchesAsync(string? courseId, bool includeExpired, string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize)
        {
            var batches = string.IsNullOrWhiteSpace(courseId)
                ? await _batchRepository.GetAllAsync()
                : await _batchRepository.GetByCourseIdAsync(courseId);

            if (!includeExpired)
            {
                batches = batches.Where(b => !b.IsExpired).ToList();
            }

            var filtered = TextSearchHelper.ApplyFilter(batches, searchTerm, globalFilter, SearchFields, DefaultSearchFields);
            return PagingHelper.ToPagedResult(filtered, pageNumber, pageSize);
        }

        public async Task<bool> DeleteBatchAsync(string id)
        {
            var batch = await _batchRepository.GetByIdAsync(id);
            if (batch == null)
            {
                throw new KeyNotFoundException(Constants.Messages.BatchNotFound);
            }

            var deleted = await _batchRepository.DeleteAsync(id);
            if (deleted)
            {
                await DeletePdfAsync(batch.PdfKey);
            }
            return deleted;
        }
    }
}

using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public class QuestionPaperService : IQuestionPaperService
    {
        private const long MaxPdfSizeInBytes = 10 * 1024 * 1024; // 10 MB

        private readonly IQuestionPaperRepository _questionPaperRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly IBatchRepository _batchRepository;
        private readonly IEnrollmentRepository _enrollmentRepository;
        private readonly IFileStorageService _fileStorageService;

        public QuestionPaperService(
            IQuestionPaperRepository questionPaperRepository,
            ICourseRepository courseRepository,
            IBatchRepository batchRepository,
            IEnrollmentRepository enrollmentRepository,
            IFileStorageService fileStorageService)
        {
            _questionPaperRepository = questionPaperRepository;
            _courseRepository = courseRepository;
            _batchRepository = batchRepository;
            _enrollmentRepository = enrollmentRepository;
            _fileStorageService = fileStorageService;
        }

        public async Task<QuestionPaper> CreateQuestionPaperAsync(QuestionPaperRequest request)
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
                throw new InvalidOperationException("Course not found");
            }

            Batch? batch = null;
            if (!string.IsNullOrWhiteSpace(request.BatchId))
            {
                batch = await _batchRepository.GetByIdAsync(request.BatchId);
                if (batch == null)
                {
                    throw new InvalidOperationException(Constants.Messages.BatchNotFound);
                }
                if (batch.CourseId != course.Id)
                {
                    throw new InvalidOperationException(Constants.Messages.BatchCourseMismatch);
                }
            }

            if (string.IsNullOrWhiteSpace(request.MaterialToView))
            {
                throw new InvalidOperationException("MaterialToView is required (Paid or Free)");
            }

            if (request.PdfFile == null || request.PdfFile.Length == 0)
            {
                throw new InvalidOperationException("PDF file is required");
            }

            ValidatePdf(request.PdfFile);
            var (pdfUrl, pdfKey) = await _fileStorageService.UploadPdfAsync(request.PdfFile, GetCourseFolder(course));

            var questionPaper = new QuestionPaper
            {
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? "" : request.Description.Trim(),
                CourseId = request.CourseId,
                BatchId = batch?.Id,
                BatchTitle = batch?.Title,
                MaterialToView = Constants.MaterialAccess.Normalize(request.MaterialToView),
                PdfUrl = pdfUrl,
                PdfFileName = pdfKey,
                CreatedAt = DateTime.UtcNow
            };

            return await _questionPaperRepository.CreateAsync(questionPaper);
        }

        private static readonly Dictionary<string, Func<QuestionPaper, string?>> SearchFields = new()
        {
            ["title"] = m => m.Title,
            ["description"] = m => m.Description,
            ["batchtitle"] = m => m.BatchTitle,
            ["materialtoview"] = m => m.MaterialToView
        };
        private static readonly string[] DefaultSearchFields = { "title", "description", "batchTitle" };

        public async Task<PagedResult<QuestionPaper>> GetAccessibleQuestionPapersAsync(
            string userId, string role, string? courseId, string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize)
        {
            var materials = await GetAccessibleQuestionPapersCoreAsync(userId, role);

            if (!string.IsNullOrWhiteSpace(courseId) && !string.Equals(courseId, "All", StringComparison.OrdinalIgnoreCase))
            {
                materials = materials.Where(m => m.CourseId == courseId).ToList();
            }

            var filtered = TextSearchHelper.ApplyFilter(materials, searchTerm, globalFilter, SearchFields, DefaultSearchFields);
            return PagingHelper.ToPagedResult(filtered, pageNumber, pageSize);
        }

        private async Task<List<QuestionPaper>> GetAccessibleQuestionPapersCoreAsync(string userId, string role)
        {
            if (role == Constants.Roles.Admin)
            {
                return await _questionPaperRepository.GetAllAsync();
            }

            // Free materials are visible to every logged-in student (no enrollment / batch check)
            var all = await _questionPaperRepository.GetAllAsync();
            var free = all.Where(IsFree).ToList();

            var verifiedEnrollments = await GetVerifiedEnrollmentsAsync(userId);
            if (verifiedEnrollments.Count == 0)
            {
                return free;
            }

            // A student can hold several verified enrollments in one course (different batches): any of them counts
            var enrolled = all
                .Where(m => verifiedEnrollments.Any(e => e.CourseId == m.CourseId && MatchesBatch(m, e)));

            return enrolled.Concat(free).GroupBy(m => m.Id).Select(g => g.First()).ToList();
        }

        private static bool IsFree(QuestionPaper m)
            => string.Equals(m.MaterialToView, Constants.MaterialAccess.Free, StringComparison.OrdinalIgnoreCase);

        public async Task<QuestionPaper> GetQuestionPaperByIdAsync(string id, string userId, string role)
        {
            var questionPaper = await _questionPaperRepository.GetByIdAsync(id);
            if (questionPaper == null)
            {
                throw new KeyNotFoundException("Question paper not found");
            }

            if (role != Constants.Roles.Admin && !IsFree(questionPaper))
            {
                var enrollments = await GetVerifiedEnrollmentsAsync(userId);
                if (!enrollments.Any(e => e.CourseId == questionPaper.CourseId && MatchesBatch(questionPaper, e)))
                {
                    throw new UnauthorizedAccessException(Constants.Messages.NoCourseAccess);
                }
            }

            return questionPaper;
        }

        public async Task<QuestionPaper> UpdateQuestionPaperAsync(string id, QuestionPaperRequest request)
        {
            var existing = await _questionPaperRepository.GetByIdAsync(id);
            if (existing == null)
            {
                throw new KeyNotFoundException("Question paper not found");
            }

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                existing.Title = request.Title.Trim();
            }

            if (request.Description != null)
            {
                existing.Description = request.Description.Trim();
            }

            Course? course = null;
            if (!string.IsNullOrWhiteSpace(request.CourseId))
            {
                course = await _courseRepository.GetByIdAsync(request.CourseId);
                if (course == null)
                {
                    throw new InvalidOperationException("Course not found");
                }
                existing.CourseId = request.CourseId;
            }

            if (!string.IsNullOrWhiteSpace(request.BatchId))
            {
                var batch = await _batchRepository.GetByIdAsync(request.BatchId);
                if (batch == null)
                {
                    throw new InvalidOperationException(Constants.Messages.BatchNotFound);
                }
                if (batch.CourseId != existing.CourseId)
                {
                    throw new InvalidOperationException(Constants.Messages.BatchCourseMismatch);
                }
                existing.BatchId = batch.Id;
                existing.BatchTitle = batch.Title;
            }

            if (!string.IsNullOrWhiteSpace(request.MaterialToView))
            {
                existing.MaterialToView = Constants.MaterialAccess.Normalize(request.MaterialToView);
            }

            if (request.PdfFile != null && request.PdfFile.Length > 0)
            {
                ValidatePdf(request.PdfFile);

                course ??= await _courseRepository.GetByIdAsync(existing.CourseId);
                if (course == null)
                {
                    throw new InvalidOperationException("Course not found");
                }

                var oldKey = existing.PdfFileName;
                var (pdfUrl, pdfKey) = await _fileStorageService.UploadPdfAsync(request.PdfFile, GetCourseFolder(course));
                existing.PdfUrl = pdfUrl;
                existing.PdfFileName = pdfKey;

                await _fileStorageService.DeleteAsync(oldKey);
            }

            existing.UpdatedAt = DateTime.UtcNow;
            await _questionPaperRepository.UpdateAsync(id, existing);
            return existing;
        }

        public async Task<bool> DeleteQuestionPaperAsync(string id)
        {
            var questionPaper = await _questionPaperRepository.GetByIdAsync(id);
            if (questionPaper == null)
            {
                throw new KeyNotFoundException("Question paper not found");
            }

            var deleted = await _questionPaperRepository.DeleteAsync(id);
            if (deleted)
            {
                await _fileStorageService.DeleteAsync(questionPaper.PdfFileName);
            }

            return deleted;
        }

        private static string GetCourseFolder(Course course)
        {
            return $"coaching/{course.CourseName.Trim().ToUpperInvariant()}";
        }

        private async Task<Enrollment?> GetVerifiedEnrollmentAsync(string userId, string courseId)
        {
            var enrollments = await _enrollmentRepository.GetByUserIdAsync(userId);
            return enrollments.FirstOrDefault(e =>
                e.CourseId == courseId && e.Status == Constants.EnrollmentStatuses.Verified);
        }

        private async Task<List<Enrollment>> GetVerifiedEnrollmentsAsync(string userId)
        {
            var enrollments = await _enrollmentRepository.GetByUserIdAsync(userId);
            return enrollments.Where(e => e.Status == Constants.EnrollmentStatuses.Verified).ToList();
        }

        private static bool MatchesBatch(QuestionPaper questionPaper, Enrollment enrollment)
        {
            return string.IsNullOrWhiteSpace(questionPaper.BatchId) || questionPaper.BatchId == enrollment.BatchId;
        }

        private static void ValidatePdf(IFormFile pdfFile)
        {
            var extension = Path.GetExtension(pdfFile.FileName);
            if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only PDF files are allowed");
            }

            if (pdfFile.Length > MaxPdfSizeInBytes)
            {
                throw new InvalidOperationException("PDF file size must not exceed 10 MB");
            }
        }
    }
}

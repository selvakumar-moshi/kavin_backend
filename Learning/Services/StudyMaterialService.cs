using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public class StudyMaterialService : IStudyMaterialService
    {
        private const long MaxPdfSizeInBytes = 10 * 1024 * 1024; // 10 MB

        private readonly IStudyMaterialRepository _studyMaterialRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly IBatchRepository _batchRepository;
        private readonly IEnrollmentRepository _enrollmentRepository;
        private readonly IFileStorageService _fileStorageService;

        public StudyMaterialService(
            IStudyMaterialRepository studyMaterialRepository,
            ICourseRepository courseRepository,
            IBatchRepository batchRepository,
            IEnrollmentRepository enrollmentRepository,
            IFileStorageService fileStorageService)
        {
            _studyMaterialRepository = studyMaterialRepository;
            _courseRepository = courseRepository;
            _batchRepository = batchRepository;
            _enrollmentRepository = enrollmentRepository;
            _fileStorageService = fileStorageService;
        }

        public async Task<StudyMaterial> CreateStudyMaterialAsync(StudyMaterialRequest request)
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

            if (request.PdfFile == null || request.PdfFile.Length == 0)
            {
                throw new InvalidOperationException("PDF file is required");
            }

            ValidatePdf(request.PdfFile);
            var (pdfUrl, pdfKey) = await _fileStorageService.UploadPdfAsync(request.PdfFile, GetCourseFolder(course));

            var studyMaterial = new StudyMaterial
            {
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? "" : request.Description.Trim(),
                CourseId = request.CourseId,
                BatchId = batch?.Id,
                BatchTitle = batch?.Title,
                MaterialToView = string.IsNullOrWhiteSpace(request.MaterialToView)
                    ? Constants.MaterialAccess.Paid
                    : Constants.MaterialAccess.Normalize(request.MaterialToView),
                PdfUrl = pdfUrl,
                PdfFileName = pdfKey,
                CreatedAt = DateTime.UtcNow
            };

            return await _studyMaterialRepository.CreateAsync(studyMaterial);
        }

        private static readonly Dictionary<string, Func<StudyMaterial, string?>> SearchFields = new()
        {
            ["title"] = m => m.Title,
            ["description"] = m => m.Description,
            ["batchtitle"] = m => m.BatchTitle
        };
        private static readonly string[] DefaultSearchFields = { "title", "description", "batchTitle" };

        public async Task<PagedResult<StudyMaterial>> GetAccessibleStudyMaterialsAsync(
            string userId, string role, string? courseId, string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize)
        {
            var materials = await GetAccessibleStudyMaterialsCoreAsync(userId, role);

            if (!string.IsNullOrWhiteSpace(courseId) && !string.Equals(courseId, "All", StringComparison.OrdinalIgnoreCase))
            {
                materials = materials.Where(m => m.CourseId == courseId).ToList();
            }

            var filtered = TextSearchHelper.ApplyFilter(materials, searchTerm, globalFilter, SearchFields, DefaultSearchFields);
            return PagingHelper.ToPagedResult(filtered, pageNumber, pageSize);
        }

        private async Task<List<StudyMaterial>> GetAccessibleStudyMaterialsCoreAsync(string userId, string role)
        {
            if (role == Constants.Roles.Admin)
            {
                return await _studyMaterialRepository.GetAllAsync();
            }

            var verifiedEnrollments = await GetVerifiedEnrollmentsAsync(userId);
            if (verifiedEnrollments.Count == 0)
            {
                return new List<StudyMaterial>();
            }

            var courseIds = verifiedEnrollments.Select(e => e.CourseId).ToList();
            var enrollmentByCourse = verifiedEnrollments.ToDictionary(e => e.CourseId);
            var allMaterials = await _studyMaterialRepository.GetByCourseIdsAsync(courseIds);

            return allMaterials
                .Where(m => enrollmentByCourse.TryGetValue(m.CourseId, out var e) && MatchesBatch(m, e))
                .ToList();
        }

        public async Task<StudyMaterial> GetStudyMaterialByIdAsync(string id, string userId, string role)
        {
            var studyMaterial = await _studyMaterialRepository.GetByIdAsync(id);
            if (studyMaterial == null)
            {
                throw new KeyNotFoundException("Study material not found");
            }

            if (role != Constants.Roles.Admin)
            {
                var enrollment = await GetVerifiedEnrollmentAsync(userId, studyMaterial.CourseId);
                if (enrollment == null || !MatchesBatch(studyMaterial, enrollment))
                {
                    throw new UnauthorizedAccessException(Constants.Messages.NoCourseAccess);
                }
            }

            return studyMaterial;
        }

        public async Task<StudyMaterial> UpdateStudyMaterialAsync(string id, StudyMaterialRequest request)
        {
            var existing = await _studyMaterialRepository.GetByIdAsync(id);
            if (existing == null)
            {
                throw new KeyNotFoundException("Study material not found");
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
            await _studyMaterialRepository.UpdateAsync(id, existing);
            return existing;
        }

        public async Task<bool> DeleteStudyMaterialAsync(string id)
        {
            var studyMaterial = await _studyMaterialRepository.GetByIdAsync(id);
            if (studyMaterial == null)
            {
                throw new KeyNotFoundException("Study material not found");
            }

            var deleted = await _studyMaterialRepository.DeleteAsync(id);
            if (deleted)
            {
                await _fileStorageService.DeleteAsync(studyMaterial.PdfFileName);
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

        private static bool MatchesBatch(StudyMaterial studyMaterial, Enrollment enrollment)
        {
            return string.IsNullOrWhiteSpace(studyMaterial.BatchId) || studyMaterial.BatchId == enrollment.BatchId;
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

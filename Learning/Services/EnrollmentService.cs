using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public class EnrollmentService : IEnrollmentService
    {
        private readonly IEnrollmentRepository _enrollmentRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly IUserRepository _userRepository;
        private readonly IExcelExportService _excelExportService;
        private readonly IFileStorageService _fileStorageService;

        private const long MaxScreenshotSizeInBytes = 5 * 1024 * 1024; // 5 MB
        private const string PaymentFolder = "coaching/payment";

        public EnrollmentService(
            IEnrollmentRepository enrollmentRepository,
            ICourseRepository courseRepository,
            IUserRepository userRepository,
            IExcelExportService excelExportService,
            IFileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService;
            _enrollmentRepository = enrollmentRepository;
            _courseRepository = courseRepository;
            _userRepository = userRepository;
            _excelExportService = excelExportService;
        }

        public async Task<List<Enrollment>> GetAllEnrollmentsAsync()
        {
            return await _enrollmentRepository.GetAllAsync();
        }

        public async Task<List<Enrollment>> GetEnrollmentsByUserIdAsync(string userId)
        {
            return await _enrollmentRepository.GetByUserIdAsync(userId);
        }

        public async Task<Enrollment> GetEnrollmentByIdAsync(string id)
        {
            var enrollment = await _enrollmentRepository.GetByIdAsync(id);
            if (enrollment == null)
            {
                throw new KeyNotFoundException(Constants.Messages.EnrollmentNotFound);
            }
            return enrollment;
        }

        public async Task<Enrollment> UpdateStatusAsync(string id, string status, string? paymentMethod, string? transactionReference, string adminUserId)
        {
            if (!Constants.EnrollmentStatuses.All.Contains(status))
            {
                throw new InvalidOperationException(Constants.Messages.InvalidEnrollmentStatus);
            }

            if (!string.IsNullOrWhiteSpace(paymentMethod) && !Constants.PaymentMethods.All.Contains(paymentMethod))
            {
                throw new InvalidOperationException(Constants.Messages.InvalidPaymentMethod);
            }

            var enrollment = await _enrollmentRepository.GetByIdAsync(id);
            if (enrollment == null)
            {
                throw new KeyNotFoundException(Constants.Messages.EnrollmentNotFound);
            }

            enrollment.Status = status;

            if (!string.IsNullOrWhiteSpace(paymentMethod))
            {
                enrollment.PaymentMethod = paymentMethod;
            }

            if (!string.IsNullOrWhiteSpace(transactionReference))
            {
                enrollment.TransactionReference = transactionReference;
            }

            if (status == Constants.EnrollmentStatuses.Verified)
            {
                enrollment.VerifiedAt = DateTime.UtcNow;
                enrollment.VerifiedByAdminId = adminUserId;
            }
            else if (status == Constants.EnrollmentStatuses.Pending || status == Constants.EnrollmentStatuses.Rejected)
            {
                // Reverting to Pending means undoing a verification mistake - clear the record of it.
                // Dropping a previously-verified enrollment is not a mistake, so its verification
                // history (proof the course was paid for) is preserved rather than wiped here.
                enrollment.VerifiedAt = null;
                enrollment.VerifiedByAdminId = null;
            }

            await _enrollmentRepository.UpdateAsync(id, enrollment);
            return enrollment;
        }

        public async Task<Enrollment> UploadPaymentAttachmentAsync(string id, string userId, IFormFile file)
        {
            var enrollment = await _enrollmentRepository.GetByIdAsync(id);
            // Same message for "missing" and "not yours" so enrollment IDs can't be probed
            if (enrollment == null || enrollment.UserId != userId)
            {
                throw new KeyNotFoundException(Constants.Messages.EnrollmentNotFound);
            }

            if (file == null || file.Length == 0)
            {
                throw new InvalidOperationException(Constants.Messages.PaymentScreenshotRequired);
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(extension))
            {
                throw new InvalidOperationException(Constants.Messages.PaymentScreenshotInvalidType);
            }

            if (file.Length > MaxScreenshotSizeInBytes)
            {
                throw new InvalidOperationException(Constants.Messages.PaymentScreenshotTooLarge);
            }

            if (enrollment.Status == Constants.EnrollmentStatuses.Verified)
            {
                throw new InvalidOperationException(Constants.Messages.PaymentAlreadyVerified);
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new KeyNotFoundException(Constants.Messages.UserNotFound);
            }

            // File is named after the student (plus application no. so two students with the same
            // name don't overwrite each other), e.g. Ajith_kumar_RSK-1004.png
            var rawName = $"{user.FirstName}_{user.LastName}_{user.ApplicationNo}".Trim('_');
            var safeName = new string(rawName.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
            var fileName = $"{safeName}{extension}";

            var oldKey = enrollment.PaymentScreenshotKey;
            var (url, key) = await _fileStorageService.UploadImageAsync(file, PaymentFolder, fileName);

            enrollment.PaymentScreenshot = url;
            enrollment.PaymentScreenshotKey = key;
            if (enrollment.Status == Constants.EnrollmentStatuses.Rejected)
            {
                // Re-submitted after rejection - send it back to the admin's review queue
                enrollment.Status = Constants.EnrollmentStatuses.Pending;
            }
            enrollment.UpdatedAt = DateTime.UtcNow;
            await _enrollmentRepository.UpdateAsync(id, enrollment);

            // Same name means the upload already overwrote the old file - deleting it would remove the new one
            if (!string.IsNullOrWhiteSpace(oldKey) && oldKey != key)
            {
                await _fileStorageService.DeleteAsync(oldKey);
            }

            return enrollment;
        }

        public async Task<(byte[] Content, string FileName)> ExportCourseEnrollmentsAsync(string courseId)
        {
            var course = await _courseRepository.GetByIdAsync(courseId);
            if (course == null)
            {
                throw new InvalidOperationException(Constants.Messages.CourseNotFound);
            }

            var enrollments = await _enrollmentRepository.GetByCourseIdAsync(courseId);
            var ordered = enrollments.OrderByDescending(e => e.CreatedAt).ToList();

            var rows = new List<CourseEnrollmentReportRow>();
            foreach (var enrollment in ordered)
            {
                var user = await _userRepository.GetByIdAsync(enrollment.UserId);

                rows.Add(new CourseEnrollmentReportRow
                {
                    FirstName = user?.FirstName ?? "Unknown",
                    LastName = user?.LastName ?? "",
                    ApplicationNo = user?.ApplicationNo,
                    Email = user?.Email ?? "",
                    PhoneNumber = user?.PhoneNumber ?? "",
                    District = user?.District,
                    BatchTitle = enrollment.BatchTitle,
                    Status = enrollment.Status
                });
            }

            var content = _excelExportService.GenerateCourseEnrollmentExcel(course.CourseName, rows);

            var invalidChars = Path.GetInvalidFileNameChars();
            var safeCourseName = new string(course.CourseName.Select(c => invalidChars.Contains(c) || c == ' ' ? '_' : c).ToArray());
            var fileName = $"Course-{safeCourseName}.xlsx";

            return (content, fileName);
        }
    }
}

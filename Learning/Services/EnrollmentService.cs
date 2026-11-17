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
        private readonly IBatchRepository _batchRepository;
        private readonly ISyllabusWiseRepository _syllabusWiseRepository;

        private const long MaxScreenshotSizeInBytes = 5 * 1024 * 1024; // 5 MB
        private const string PaymentFolder = "coaching/payment";

        public EnrollmentService(
            IEnrollmentRepository enrollmentRepository,
            ICourseRepository courseRepository,
            IUserRepository userRepository,
            IExcelExportService excelExportService,
            IFileStorageService fileStorageService,
            IBatchRepository batchRepository,
            ISyllabusWiseRepository syllabusWiseRepository)
        {
            _syllabusWiseRepository = syllabusWiseRepository;
            _batchRepository = batchRepository;
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
                transactionReference = transactionReference.Trim().ToUpperInvariant();
                await EnsureReferenceNotUsedAsync(transactionReference, enrollment.Id);
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

        public async Task<Enrollment> EnrollAsync(string userId, string? courseId, string? batchId)
        {
            if (string.IsNullOrWhiteSpace(courseId))
            {
                throw new InvalidOperationException("Course is required");
            }

            if (string.IsNullOrWhiteSpace(batchId))
            {
                throw new InvalidOperationException("Batch is required");
            }

            var course = await _courseRepository.GetByIdAsync(courseId);
            if (course == null)
            {
                throw new InvalidOperationException(Constants.Messages.CourseNotFound);
            }

            var batch = await _batchRepository.GetByIdAsync(batchId);
            if (batch == null)
            {
                throw new InvalidOperationException(Constants.Messages.BatchNotFound);
            }

            if (batch.CourseId != course.Id)
            {
                throw new InvalidOperationException(Constants.Messages.BatchCourseMismatch);
            }

            if (batch.IsExpired)
            {
                throw new InvalidOperationException(Constants.Messages.BatchExpired);
            }

            // A student can't pick a different batch (or re-activate a drop) on their own - once
            // enrolled in a course, batch changes and rejoins go through the admin.
            var existing = (await _enrollmentRepository.GetByUserIdAsync(userId))
                .Where(e => e.CourseId == course.Id)
                .ToList();
            if (existing.Any(e => e.Status != Constants.EnrollmentStatuses.Dropped))
            {
                throw new InvalidOperationException(Constants.Messages.CourseAlreadyEnrolled);
            }
            if (existing.Count > 0)
            {
                throw new InvalidOperationException(Constants.Messages.EnrollmentDropped);
            }

            var enrollment = new Enrollment
            {
                UserId = userId,
                CourseId = course.Id,
                CourseName = course.CourseName,
                BatchId = batch.Id,
                BatchTitle = batch.Title,
                CourseAmount = course.CourseAmount,
                TotalAmount = course.CourseAmount,
                // A free course (amount 0) has nothing to pay or verify
                Status = course.IsFree ? Constants.EnrollmentStatuses.Verified : Constants.EnrollmentStatuses.Pending,
                VerifiedAt = course.IsFree ? DateTime.UtcNow : null
            };

            return await _enrollmentRepository.CreateAsync(enrollment);
        }

        public async Task<Enrollment> UploadPaymentAttachmentAsync(string id, string userId, IFormFile file, string? transactionId)
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

            // The student sends the transaction ID (UPI reference / UTR) together with the screenshot. It is checked before anything
            // is stored, so an ID that was already used leaves no file behind.
            var reference = NormalizeReference(transactionId);
            await EnsureReferenceNotUsedAsync(reference, enrollment.Id);

            // File is named after the student (plus application no. so two students with the same
            // name don't overwrite each other), e.g. Ajith_kumar_RSK-1004.png
            var rawName = $"{user.FirstName}_{user.LastName}_{user.ApplicationNo}".Trim('_');
            var safeName = new string(rawName.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
            var fileName = $"{safeName}{extension}";

            var oldKey = enrollment.PaymentScreenshotKey;
            var (url, key) = await _fileStorageService.UploadImageAsync(file, PaymentFolder, fileName);

            enrollment.PaymentScreenshot = url;
            enrollment.PaymentScreenshotKey = key;
            enrollment.TransactionReference = reference;
            enrollment.PaymentMethod ??= Constants.PaymentMethods.Upi;
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

        // 6-35 letters or digits (UPI reference / UTR), stored in upper case
        private static string NormalizeReference(string? transactionId)
        {
            var reference = (transactionId ?? "").Trim().ToUpperInvariant();
            if (reference.Length < 6 || reference.Length > 35 || !reference.All(char.IsLetterOrDigit))
            {
                throw new InvalidOperationException(Constants.Messages.TransactionReferenceInvalid);
            }
            return reference;
        }

        // One real payment must not unlock several enrollments (or a syllabus wise payment): an ID that belongs to any other payment is refused
        private async Task EnsureReferenceNotUsedAsync(string reference, string? enrollmentId)
        {
            var owner = await _enrollmentRepository.GetByTransactionReferenceAsync(reference);
            if (owner != null && owner.Id != enrollmentId)
            {
                throw new InvalidOperationException(Constants.Messages.TransactionReferenceInUse);
            }
            if (await _syllabusWiseRepository.GetPaymentByTransactionReferenceAsync(reference) != null)
            {
                throw new InvalidOperationException(Constants.Messages.TransactionReferenceInUse);
            }
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

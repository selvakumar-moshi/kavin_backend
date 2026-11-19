using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public class UserService : IUserService
    {
        private const long MaxImageSizeInBytes = 5 * 1024 * 1024; // 5 MB
        private const string ProfileImageFolder = "coaching/profile";

        private readonly IUserRepository _userRepository;
        private readonly IEnrollmentRepository _enrollmentRepository;
        private readonly IStudyMaterialRepository _studyMaterialRepository;
        private readonly IQuestionPaperRepository _questionPaperRepository;
        private readonly IVideoMaterialRepository _videoMaterialRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly IBatchRepository _batchRepository;
        private readonly IFileStorageService _fileStorageService;
        private readonly ISyllabusWiseRepository _syllabusWiseRepository;
        private readonly ISyllabusMaterialService _syllabusMaterialService;
        private readonly IDeviceTokenRepository _deviceTokenRepository;
        private readonly IExcelExportService _excelExportService;

        public UserService(
            IUserRepository userRepository,
            IEnrollmentRepository enrollmentRepository,
            IStudyMaterialRepository studyMaterialRepository,
            IQuestionPaperRepository questionPaperRepository,
            IVideoMaterialRepository videoMaterialRepository,
            ICourseRepository courseRepository,
            IBatchRepository batchRepository,
            IFileStorageService fileStorageService,
            ISyllabusWiseRepository syllabusWiseRepository,
            ISyllabusMaterialService syllabusMaterialService,
            IDeviceTokenRepository deviceTokenRepository,
            IExcelExportService excelExportService)
        {
            _excelExportService = excelExportService;
            _deviceTokenRepository = deviceTokenRepository;
            _syllabusMaterialService = syllabusMaterialService;
            _syllabusWiseRepository = syllabusWiseRepository;
            _userRepository = userRepository;
            _enrollmentRepository = enrollmentRepository;
            _studyMaterialRepository = studyMaterialRepository;
            _questionPaperRepository = questionPaperRepository;
            _videoMaterialRepository = videoMaterialRepository;
            _courseRepository = courseRepository;
            _batchRepository = batchRepository;
            _fileStorageService = fileStorageService;
        }

        public async Task<PagedResult<UserDto>> GetAllUsersAsync(UserSearchRequest? request)
        {
            var pageNumber = request?.PageNumber > 0 ? request.PageNumber : 1;
            var pageSize = request?.PageSize > 0 ? request.PageSize : 10;

            var hasSearchTerm = !string.IsNullOrWhiteSpace(request?.SearchTerm);
            var hasGlobalFilter = request?.GlobalFilter != null && request.GlobalFilter.Any(kv => !string.IsNullOrWhiteSpace(kv.Value));

            var hasDateFilter = request != null &&
                (request.PaymentUploadedFrom.HasValue || request.PaymentUploadedTo.HasValue || request.JoinedFrom.HasValue || request.JoinedTo.HasValue);

            var (users, totalCount) = hasSearchTerm || hasGlobalFilter || hasDateFilter
                ? await _userRepository.SearchAsync(request!.SearchTerm, request.GlobalFilter, pageNumber, pageSize,
                    request.PaymentUploadedFrom, request.PaymentUploadedTo, request.JoinedFrom, request.JoinedTo)
                : await _userRepository.GetAllAsync(pageNumber, pageSize);

            var result = new List<UserDto>();
            foreach (var user in users)
            {
                var dto = MapToUserDto(user);

                var enrollments = await _enrollmentRepository.GetByUserIdAsync(user.Id);
                var latestEnrollment = enrollments.OrderByDescending(e => e.CreatedAt).FirstOrDefault();
                dto.EnrollmentId = latestEnrollment?.Id;
                dto.EnrollmentStatus = latestEnrollment?.Status;
                dto.PaymentUploadedAt = user.PaymentUploadedAt;
                dto.SavedDeviceId = user.SavedDeviceId;
                dto.DeviceRegisteredAt = user.DeviceRegisteredAt;
                dto.LastLoginDate = user.LastLoginDate;

                result.Add(dto);
            }

            return new PagedResult<UserDto>
            {
                Items = result,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<UserDto> GetUserByIdAsync(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                throw new KeyNotFoundException("User not found");
            }
            return MapToUserDto(user);
        }

        public async Task<UserProfileResponse> GetUserProfileAsync(string id, bool includeAllowedBatches = false)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                throw new KeyNotFoundException("User not found");
            }

            var enrollments = await _enrollmentRepository.GetByUserIdAsync(id);
            var courses = new List<EnrolledCourseDto>();

            foreach (var courseGroup in enrollments.GroupBy(e => e.CourseId).OrderBy(g => g.Min(e => e.CreatedAt)))
            {
                var ordered = courseGroup.OrderBy(e => e.CreatedAt).ToList();
                var batches = new List<EnrolledBatchDto>();

                // Content is only loaded when some enrollment is Verified, and each batch only gets its own content (or content with no batch)
                var anyVerified = ordered.Any(e => e.Status == Constants.EnrollmentStatuses.Verified);
                var courseStudyMaterials = anyVerified
                    ? await _studyMaterialRepository.GetByCourseIdAsync(courseGroup.Key)
                    : new List<StudyMaterial>();
                var courseQuestionPapers = anyVerified
                    ? await _questionPaperRepository.GetByCourseIdAsync(courseGroup.Key)
                    : new List<QuestionPaper>();
                var courseVideoMaterials = anyVerified
                    ? await _videoMaterialRepository.GetByCourseIdAsync(courseGroup.Key)
                    : new List<VideoMaterial>();

                foreach (var enrollment in ordered)
                {
                    var isVerified = enrollment.Status == Constants.EnrollmentStatuses.Verified;
                    var batchId = enrollment.BatchId;
                    bool InBatch(string? contentBatchId) => string.IsNullOrWhiteSpace(contentBatchId) || contentBatchId == batchId;

                    batches.Add(new EnrolledBatchDto
                    {
                        EnrollmentId = enrollment.Id,
                        BatchId = enrollment.BatchId,
                        BatchTitle = enrollment.BatchTitle,
                        Amount = enrollment.CourseAmount,
                        PaymentMethod = enrollment.PaymentMethod,
                        TransactionReference = enrollment.TransactionReference,
                        PaymentScreenshot = enrollment.PaymentScreenshot,
                        EnrollmentStatus = enrollment.Status,
                        VerifiedAt = enrollment.VerifiedAt,
                        EnrolledAt = enrollment.CreatedAt,
                        TransferredFromBatchId = enrollment.TransferredFromBatchId,
                        TransferredFromBatchTitle = enrollment.TransferredFromBatchTitle,
                        TransferReason = enrollment.TransferReason,
                        TransferredAt = enrollment.TransferredAt,
                        StudyMaterials = isVerified ? courseStudyMaterials.Where(m => InBatch(m.BatchId)).ToList() : new List<StudyMaterial>(),
                        QuestionPapers = isVerified ? courseQuestionPapers.Where(m => InBatch(m.BatchId)).ToList() : new List<QuestionPaper>(),
                        VideoMaterials = isVerified ? courseVideoMaterials.Where(m => InBatch(m.BatchId)).ToList() : new List<VideoMaterial>()
                    });
                }

                List<AllowedBatchDto>? allowedBatches = null;
                if (includeAllowedBatches)
                {
                    var joined = ordered.Where(e => e.Status != Constants.EnrollmentStatuses.Dropped).Select(e => e.BatchId).ToHashSet();
                    allowedBatches = (await _batchRepository.GetByCourseIdAsync(courseGroup.Key))
                        .Where(b => !b.IsExpired && !joined.Contains(b.Id))
                        .OrderBy(b => b.BatchFrom)
                        .Select(b => new AllowedBatchDto
                        {
                            BatchId = b.Id,
                            Title = b.Title,
                            BatchFrom = b.BatchFrom,
                            BatchTo = b.BatchTo
                        })
                        .ToList();
                }

                courses.Add(new EnrolledCourseDto
                {
                    CourseId = courseGroup.Key,
                    CourseName = ordered[0].CourseName,
                    Batches = batches,
                    AllowedBatches = allowedBatches
                });
            }

            return new UserProfileResponse
            {
                Id = user.Id,
                ApplicationNo = user.ApplicationNo,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                District = user.District,
                Role = user.Role,
                ProfileImage = user.ProfileImage ?? "",
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                SavedDeviceId = includeAllowedBatches ? user.SavedDeviceId : null,
                DeviceRegisteredAt = includeAllowedBatches ? user.DeviceRegisteredAt : null,
                LastLoginDate = includeAllowedBatches ? user.LastLoginDate : null,
                Courses = courses,
                SyllabusWisePayments = (await _syllabusWiseRepository.GetPaymentsAsync(id)).Select(SyllabusWiseService.ToDto).ToList(),
                SyllabusMaterials = await _syllabusMaterialService.GetPurchasedAsync(id)
            };
        }

        // Passwords are stored as BCrypt hashes and can't be read back, so for a student who forgot theirs the admin
        // sets a new temporary one. It is returned once here and never stored in readable form.
        public async Task<string> ResetPasswordAsync(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                throw new KeyNotFoundException("User not found");
            }
            if (user.Role == Constants.Roles.Admin)
            {
                throw new InvalidOperationException("An admin's password can't be reset from here");
            }

            var password = GenerateTemporaryPassword();
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            user.MustChangePassword = true;
            await _userRepository.UpdateAsync(id, user);
            return password;
        }

        // Every student (admins are left out), oldest first
        public async Task<(byte[] Content, string FileName)> ExportUsersAsync()
        {
            var users = await _userRepository.GetAllByRoleAsync(Constants.Roles.User);
            return (_excelExportService.GenerateUserListExcel(users), $"Users_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx");
        }

        // Clears the student's registered device so the next login (from any device) registers a new one
        public async Task ResetDeviceAsync(string id)
        {
            if (!await _userRepository.ClearDeviceAsync(id))
            {
                throw new KeyNotFoundException("User not found");
            }
        }

        // 10 characters that satisfy the registration rules (upper, lower, digit, one of @$!%*?&), without look-alikes like O/0 and l/1
        private static string GenerateTemporaryPassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnpqrstuvwxyz";
            const string digits = "23456789";
            const string special = "@$!%*?&";
            const string all = upper + lower + digits + special;

            static char Pick(string chars) => chars[System.Security.Cryptography.RandomNumberGenerator.GetInt32(chars.Length)];

            var chars = new List<char> { Pick(upper), Pick(lower), Pick(digits), Pick(special) };
            while (chars.Count < 10)
            {
                chars.Add(Pick(all));
            }

            // Shuffle so the guaranteed characters aren't always first
            for (var i = chars.Count - 1; i > 0; i--)
            {
                var j = System.Security.Cryptography.RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }
            return new string(chars.ToArray());
        }

        public async Task<UserDto> UpdateProfileImageAsync(string id, IFormFile file)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                throw new KeyNotFoundException("User not found");
            }

            if (file == null || file.Length == 0)
            {
                throw new InvalidOperationException("Image file is required");
            }

            var extension = Path.GetExtension(file.FileName);
            if (!new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(extension.ToLowerInvariant()))
            {
                throw new InvalidOperationException("Only JPG, PNG, or WEBP images are allowed");
            }

            if (file.Length > MaxImageSizeInBytes)
            {
                throw new InvalidOperationException("Image file size must not exceed 5 MB");
            }

            var oldKey = user.ProfileImageKey;
            var (url, key) = await _fileStorageService.UploadImageAsync(file, ProfileImageFolder);
            user.ProfileImage = url;
            user.ProfileImageKey = key;

            await _userRepository.UpdateAsync(id, user);

            if (!string.IsNullOrWhiteSpace(oldKey))
            {
                await _fileStorageService.DeleteAsync(oldKey);
            }

            return MapToUserDto(user);
        }

        public async Task<UserDto> UpdateUserAsync(string id, UpdateUserRequest request, bool allowCourseEnrollment = false, string? adminUserId = null)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                throw new KeyNotFoundException("User not found");
            }

            if (!string.IsNullOrWhiteSpace(request.FirstName))
            {
                user.FirstName = request.FirstName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.LastName))
            {
                user.LastName = request.LastName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                user.PhoneNumber = request.PhoneNumber.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.District))
            {
                user.District = request.District.Trim();
            }

            await _userRepository.UpdateAsync(id, user);

            if (allowCourseEnrollment && request.Courses != null && request.Courses.Count > 0)
            {
                await AddCourseEnrollmentsAsync(id, request.Courses, adminUserId);
            }

            return MapToUserDto(user);
        }

        // Admin adds or moves a student into a batch. Every batch the student is in is its own enrollment (own amount, payment,
        // status), so the course price at the time of joining is kept per batch.
        //  - default (mode left out or "add"): the new batch is IN ADDITION to the batches the student already has, at today's
        //    course price. Pending until paid; paymentRequired = false grants it free. Nothing the student already has is touched.
        //  - mode "transfer": the new batch REPLACES a current one. The old batch is kept as a Dropped record (with what it was
        //    bought for) and the new batch is a new record that says where it came from. Free by default; paymentRequired = true
        //    makes it Pending at today's price until the new payment is verified.
        //  - first enrollment in the course (or rejoining after a drop): Pending (paid course) unless paymentRequired = false.
        private async Task AddCourseEnrollmentsAsync(string userId, List<CourseEnrollmentRequest> courses, string? adminUserId)
        {
            var existingEnrollments = await _enrollmentRepository.GetByUserIdAsync(userId);

            foreach (var request in courses)
            {
                if (string.IsNullOrWhiteSpace(request.CourseId) || string.IsNullOrWhiteSpace(request.BatchId))
                {
                    throw new InvalidOperationException(Constants.Messages.BatchNotFound);
                }

                var mode = (request.Mode ?? "").Trim().ToLowerInvariant();
                if (mode != "" && mode != "transfer" && mode != "add")
                {
                    throw new InvalidOperationException("mode must be transfer or add");
                }
                var isTransfer = mode == "transfer";

                var course = await _courseRepository.GetByIdAsync(request.CourseId);
                if (course == null)
                {
                    throw new InvalidOperationException(Constants.Messages.CourseNotFound);
                }

                var batch = await _batchRepository.GetByIdAsync(request.BatchId);
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

                var courseEnrollments = existingEnrollments.Where(e => e.CourseId == course.Id).ToList();
                var active = courseEnrollments.Where(e => e.Status != Constants.EnrollmentStatuses.Dropped).ToList();

                if (active.Any(e => e.BatchId == batch.Id))
                {
                    throw new InvalidOperationException("User is already enrolled in this batch");
                }

                var mustPay = request.PaymentRequired == true && course.CourseAmount > 0;

                if (active.Count > 0 && isTransfer)
                {
                    // Transfer: the new batch replaces one the student is in
                    Enrollment source;
                    if (active.Count == 1)
                    {
                        source = active[0];
                    }
                    else
                    {
                        source = active.FirstOrDefault(e => e.BatchId == request.FromBatchId)
                            ?? throw new InvalidOperationException("The student is in several batches of this course - send fromBatchId (the batch being replaced), or leave mode out to add this batch as well");
                    }

                    if (source.Status != Constants.EnrollmentStatuses.Verified)
                    {
                        // Never paid (Pending / Rejected): nothing to keep, the same record just points at the new batch
                        ApplyTransfer(source, batch, request, adminUserId);
                        await _enrollmentRepository.UpdateAsync(source.Id, source);
                    }
                    else
                    {
                        existingEnrollments.Add(await MoveToBatchAsync(source, course, batch, request, adminUserId, mustPay));
                    }
                    continue;
                }

                if (active.Count == 0 && courseEnrollments.Count > 0)
                {
                    // Rejoin after a drop: the dropped record stays as history, and the new batch is a new record that carries over
                    // what the student had paid (free by default, same as a transfer)
                    var dropped = courseEnrollments.OrderByDescending(e => e.CreatedAt).First();
                    existingEnrollments.Add(await MoveToBatchAsync(dropped, course, batch, request, adminUserId, mustPay));
                    continue;
                }

                // New enrollment (first one, or an extra batch): the amount is the course price today, kept with this batch only
                var granted = course.IsFree || request.PaymentRequired == false;
                var enrollment = new Enrollment
                {
                    UserId = userId,
                    CourseId = course.Id,
                    CourseName = course.CourseName,
                    BatchId = batch.Id,
                    BatchTitle = batch.Title,
                    CourseAmount = course.CourseAmount,
                    TotalAmount = course.CourseAmount,
                    Status = granted ? Constants.EnrollmentStatuses.Verified : Constants.EnrollmentStatuses.Pending,
                    VerifiedAt = granted ? DateTime.UtcNow : null,
                    VerifiedByAdminId = granted ? adminUserId : null
                };

                existingEnrollments.Add(await _enrollmentRepository.CreateAsync(enrollment));
            }
        }

        // The student leaves one batch for another without losing the record of the first: the old enrollment is kept as Dropped
        // (its amount, payment and screenshot stay on it) and the new batch is a new enrollment that notes where it came from.
        // Free move: the new batch keeps the amount the student paid and starts Verified. Payment required: it starts Pending at
        // today's course price until the student uploads a screenshot and the admin verifies it.
        private async Task<Enrollment> MoveToBatchAsync(Enrollment source, Course course, Batch batch, CourseEnrollmentRequest request, string? adminUserId, bool mustPay)
        {
            var amount = mustPay ? course.CourseAmount : source.CourseAmount;
            var moved = new Enrollment
            {
                UserId = source.UserId,
                CourseId = course.Id,
                CourseName = course.CourseName,
                BatchId = batch.Id,
                BatchTitle = batch.Title,
                CourseAmount = amount,
                TotalAmount = amount,
                Status = mustPay ? Constants.EnrollmentStatuses.Pending : Constants.EnrollmentStatuses.Verified,
                VerifiedAt = mustPay ? null : DateTime.UtcNow,
                VerifiedByAdminId = mustPay ? null : adminUserId,
                TransferredFromBatchId = source.BatchId,
                TransferredFromBatchTitle = source.BatchTitle,
                TransferReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                TransferredAt = DateTime.UtcNow,
                TransferredByAdminId = adminUserId
            };
            moved = await _enrollmentRepository.CreateAsync(moved);

            if (source.Status != Constants.EnrollmentStatuses.Dropped)
            {
                source.Status = Constants.EnrollmentStatuses.Dropped;
                source.UpdatedAt = DateTime.UtcNow;
                await _enrollmentRepository.UpdateAsync(source.Id, source);
            }
            return moved;
        }

        // Moves the enrollment to the new batch and records where it came from and why
        private static void ApplyTransfer(Enrollment enrollment, Batch batch, CourseEnrollmentRequest request, string? adminUserId)
        {
            enrollment.TransferredFromBatchId = enrollment.BatchId;
            enrollment.TransferredFromBatchTitle = enrollment.BatchTitle;
            enrollment.TransferReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            enrollment.TransferredAt = DateTime.UtcNow;
            enrollment.TransferredByAdminId = adminUserId;
            enrollment.BatchId = batch.Id;
            enrollment.BatchTitle = batch.Title;
            enrollment.UpdatedAt = DateTime.UtcNow;
        }

        public async Task<bool> DeleteUserAsync(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                throw new KeyNotFoundException("User not found");
            }

            var deleted = await _userRepository.DeleteAsync(id);
            if (deleted)
            {
                // A deleted student's phones must stop receiving pushes
                await _deviceTokenRepository.RemoveByUserAsync(id);
                var enrollments = await _enrollmentRepository.GetByUserIdAsync(id);
                foreach (var enrollment in enrollments)
                {
                    await _enrollmentRepository.DeleteAsync(enrollment.Id);
                }
            }

            return deleted;
        }

        private static UserDto MapToUserDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                ApplicationNo = user.ApplicationNo,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                District = user.District,
                Role = user.Role,
                ProfileImage = user.ProfileImage ?? "",
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                MustChangePassword = user.MustChangePassword
            };
        }
    }
}

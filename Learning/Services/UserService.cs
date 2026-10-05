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
        private readonly IVideoMaterialRepository _videoMaterialRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly IBatchRepository _batchRepository;
        private readonly IFileStorageService _fileStorageService;

        public UserService(
            IUserRepository userRepository,
            IEnrollmentRepository enrollmentRepository,
            IStudyMaterialRepository studyMaterialRepository,
            IVideoMaterialRepository videoMaterialRepository,
            ICourseRepository courseRepository,
            IBatchRepository batchRepository,
            IFileStorageService fileStorageService)
        {
            _userRepository = userRepository;
            _enrollmentRepository = enrollmentRepository;
            _studyMaterialRepository = studyMaterialRepository;
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

            var (users, totalCount) = hasSearchTerm || hasGlobalFilter
                ? await _userRepository.SearchAsync(request!.SearchTerm, request.GlobalFilter, pageNumber, pageSize)
                : await _userRepository.GetAllAsync(pageNumber, pageSize);

            var result = new List<UserDto>();
            foreach (var user in users)
            {
                var dto = MapToUserDto(user);

                var enrollments = await _enrollmentRepository.GetByUserIdAsync(user.Id);
                var latestEnrollment = enrollments.OrderByDescending(e => e.CreatedAt).FirstOrDefault();
                dto.EnrollmentId = latestEnrollment?.Id;
                dto.EnrollmentStatus = latestEnrollment?.Status;

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

            var batchesByCourse = new Dictionary<string, List<Batch>>();

            foreach (var enrollment in enrollments)
            {
                var isVerified = enrollment.Status == Constants.EnrollmentStatuses.Verified;
                var studyMaterials = isVerified
                    ? await _studyMaterialRepository.GetByCourseIdAsync(enrollment.CourseId)
                    : new List<StudyMaterial>();
                var videoMaterials = isVerified
                    ? await _videoMaterialRepository.GetByCourseIdAsync(enrollment.CourseId)
                    : new List<VideoMaterial>();

                List<AllowedBatchDto>? allowedBatches = null;
                if (includeAllowedBatches)
                {
                    if (!batchesByCourse.TryGetValue(enrollment.CourseId, out var courseBatches))
                    {
                        courseBatches = await _batchRepository.GetByCourseIdAsync(enrollment.CourseId);
                        batchesByCourse[enrollment.CourseId] = courseBatches;
                    }

                    allowedBatches = courseBatches
                        .Where(b => !b.IsExpired && b.Id != enrollment.BatchId)
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
                    EnrollmentId = enrollment.Id,
                    CourseId = enrollment.CourseId,
                    CourseName = enrollment.CourseName,
                    BatchId = enrollment.BatchId,
                    BatchTitle = enrollment.BatchTitle,
                    CourseAmount = enrollment.CourseAmount,
                    TotalAmount = enrollment.TotalAmount,
                    PaymentMethod = enrollment.PaymentMethod,
                    TransactionReference = enrollment.TransactionReference,
                    PaymentScreenshot = enrollment.PaymentScreenshot,
                    EnrollmentStatus = enrollment.Status,
                    VerifiedAt = enrollment.VerifiedAt,
                    StudyMaterials = studyMaterials,
                    VideoMaterials = videoMaterials,
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
                Courses = courses
            };
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

        private async Task AddCourseEnrollmentsAsync(string userId, List<CourseEnrollmentRequest> courses, string? adminUserId)
        {
            var existingEnrollments = await _enrollmentRepository.GetByUserIdAsync(userId);
            var enrollmentsByCourse = existingEnrollments
                .GroupBy(e => e.CourseId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var request in courses)
            {
                if (string.IsNullOrWhiteSpace(request.CourseId) || string.IsNullOrWhiteSpace(request.BatchId))
                {
                    throw new InvalidOperationException(Constants.Messages.BatchNotFound);
                }

                // An existing enrollment for this course is never duplicated - the same record is
                // reused, since the course was already paid for:
                //  - Pending/Verified/Rejected (still active): the student is moved to the new batch,
                //    keeping their status and payment details. Same batch -> "already enrolled".
                //  - Dropped: the student is rejoining, so it is re-verified in the new batch.
                Enrollment? activeEnrollment = null;
                Enrollment? droppedEnrollment = null;
                if (enrollmentsByCourse.TryGetValue(request.CourseId, out var courseEnrollments))
                {
                    activeEnrollment = courseEnrollments.FirstOrDefault(e => e.Status != Constants.EnrollmentStatuses.Dropped);
                    if (activeEnrollment == null)
                    {
                        droppedEnrollment = courseEnrollments.OrderByDescending(e => e.CreatedAt).First();
                    }
                }

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

                if (activeEnrollment != null)
                {
                    if (activeEnrollment.BatchId == batch.Id)
                    {
                        throw new InvalidOperationException(Constants.Messages.CourseAlreadyEnrolled);
                    }

                    // Batch change: only the batch moves - status, payment and verification stay.
                    activeEnrollment.BatchId = batch.Id;
                    activeEnrollment.BatchTitle = batch.Title;

                    await _enrollmentRepository.UpdateAsync(activeEnrollment.Id, activeEnrollment);
                    continue;
                }

                if (droppedEnrollment != null)
                {
                    // Rejoin: update the existing enrollment in place. CourseAmount/TotalAmount/
                    // PaymentMethod/TransactionReference are left untouched - no re-payment needed.
                    droppedEnrollment.BatchId = batch.Id;
                    droppedEnrollment.BatchTitle = batch.Title;
                    droppedEnrollment.Status = Constants.EnrollmentStatuses.Verified;
                    droppedEnrollment.VerifiedAt = DateTime.UtcNow;
                    droppedEnrollment.VerifiedByAdminId = adminUserId;

                    await _enrollmentRepository.UpdateAsync(droppedEnrollment.Id, droppedEnrollment);
                    continue;
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
                    Status = Constants.EnrollmentStatuses.Pending
                };

                await _enrollmentRepository.CreateAsync(enrollment);
            }
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
                UpdatedAt = user.UpdatedAt
            };
        }
    }
}

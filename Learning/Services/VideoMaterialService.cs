using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public class VideoMaterialService : IVideoMaterialService
    {
        private readonly IVideoMaterialRepository _videoMaterialRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly IBatchRepository _batchRepository;
        private readonly IEnrollmentRepository _enrollmentRepository;

        public VideoMaterialService(
            IVideoMaterialRepository videoMaterialRepository,
            ICourseRepository courseRepository,
            IBatchRepository batchRepository,
            IEnrollmentRepository enrollmentRepository)
        {
            _videoMaterialRepository = videoMaterialRepository;
            _courseRepository = courseRepository;
            _batchRepository = batchRepository;
            _enrollmentRepository = enrollmentRepository;
        }

        public async Task<VideoMaterial> CreateVideoMaterialAsync(VideoMaterialRequest request)
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

            ValidateYoutubeLink(request.YoutubeLink);

            var videoMaterial = new VideoMaterial
            {
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? "" : request.Description.Trim(),
                CourseId = request.CourseId,
                BatchId = batch?.Id,
                BatchTitle = batch?.Title,
                YoutubeLink = request.YoutubeLink!.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            return await _videoMaterialRepository.CreateAsync(videoMaterial);
        }

        private static readonly Dictionary<string, Func<VideoMaterial, string?>> SearchFields = new()
        {
            ["title"] = m => m.Title,
            ["description"] = m => m.Description,
            ["batchtitle"] = m => m.BatchTitle
        };
        private static readonly string[] DefaultSearchFields = { "title", "description", "batchTitle" };

        public async Task<PagedResult<VideoMaterial>> GetAccessibleVideoMaterialsAsync(
            string userId, string role, string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize)
        {
            var materials = await GetAccessibleVideoMaterialsCoreAsync(userId, role);
            var filtered = TextSearchHelper.ApplyFilter(materials, searchTerm, globalFilter, SearchFields, DefaultSearchFields);
            return PagingHelper.ToPagedResult(filtered, pageNumber, pageSize);
        }

        private async Task<List<VideoMaterial>> GetAccessibleVideoMaterialsCoreAsync(string userId, string role)
        {
            if (role == Constants.Roles.Admin)
            {
                return await _videoMaterialRepository.GetAllAsync();
            }

            var verifiedEnrollments = await GetVerifiedEnrollmentsAsync(userId);
            if (verifiedEnrollments.Count == 0)
            {
                return new List<VideoMaterial>();
            }

            var courseIds = verifiedEnrollments.Select(e => e.CourseId).ToList();
            var enrollmentByCourse = verifiedEnrollments.ToDictionary(e => e.CourseId);
            var allMaterials = await _videoMaterialRepository.GetByCourseIdsAsync(courseIds);

            return allMaterials
                .Where(m => enrollmentByCourse.TryGetValue(m.CourseId, out var e) && MatchesBatch(m, e))
                .ToList();
        }

        public async Task<VideoMaterial> GetVideoMaterialByIdAsync(string id, string userId, string role)
        {
            var videoMaterial = await _videoMaterialRepository.GetByIdAsync(id);
            if (videoMaterial == null)
            {
                throw new KeyNotFoundException("Video material not found");
            }

            if (role != Constants.Roles.Admin)
            {
                var enrollment = await GetVerifiedEnrollmentAsync(userId, videoMaterial.CourseId);
                if (enrollment == null || !MatchesBatch(videoMaterial, enrollment))
                {
                    throw new UnauthorizedAccessException(Constants.Messages.NoCourseAccess);
                }
            }

            return videoMaterial;
        }

        public async Task<VideoMaterial> UpdateVideoMaterialAsync(string id, VideoMaterialRequest request)
        {
            var existing = await _videoMaterialRepository.GetByIdAsync(id);
            if (existing == null)
            {
                throw new KeyNotFoundException("Video material not found");
            }

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                existing.Title = request.Title.Trim();
            }

            if (request.Description != null)
            {
                existing.Description = request.Description.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.CourseId))
            {
                var course = await _courseRepository.GetByIdAsync(request.CourseId);
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

            if (!string.IsNullOrWhiteSpace(request.YoutubeLink))
            {
                ValidateYoutubeLink(request.YoutubeLink);
                existing.YoutubeLink = request.YoutubeLink.Trim();
            }

            existing.UpdatedAt = DateTime.UtcNow;
            await _videoMaterialRepository.UpdateAsync(id, existing);
            return existing;
        }

        public async Task<bool> DeleteVideoMaterialAsync(string id)
        {
            var videoMaterial = await _videoMaterialRepository.GetByIdAsync(id);
            if (videoMaterial == null)
            {
                throw new KeyNotFoundException("Video material not found");
            }

            return await _videoMaterialRepository.DeleteAsync(id);
        }

        private static void ValidateYoutubeLink(string? youtubeLink)
        {
            if (string.IsNullOrWhiteSpace(youtubeLink) ||
                !Uri.TryCreate(youtubeLink, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("A valid YouTube link is required");
            }
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

        private static bool MatchesBatch(VideoMaterial videoMaterial, Enrollment enrollment)
        {
            return string.IsNullOrWhiteSpace(videoMaterial.BatchId) || videoMaterial.BatchId == enrollment.BatchId;
        }
    }
}

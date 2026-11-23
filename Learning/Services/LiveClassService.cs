using System.Globalization;
using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public class LiveClassService : ILiveClassService
    {
        private readonly ILiveClassRepository _repository;
        private readonly ICourseRepository _courseRepository;
        private readonly IBatchRepository _batchRepository;
        private readonly IEnrollmentRepository _enrollmentRepository;
        private readonly TimeZoneInfo _timeZone;

        public LiveClassService(
            ILiveClassRepository repository,
            ICourseRepository courseRepository,
            IBatchRepository batchRepository,
            IEnrollmentRepository enrollmentRepository,
            IConfiguration configuration)
        {
            _repository = repository;
            _courseRepository = courseRepository;
            _batchRepository = batchRepository;
            _enrollmentRepository = enrollmentRepository;
            _timeZone = ScheduleHelper.ResolveTimeZone(configuration["LiveClass:TimeZone"]);
        }

        private DateTime LocalNow() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);

        public async Task<LiveClass> CreateAsync(LiveClassRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                throw new InvalidOperationException("Title is required");
            }

            var date = ScheduleHelper.ParseDate(request.Date);
            var start = ScheduleHelper.ParseTime(request.StartTime, "Start time");
            var end = ScheduleHelper.ParseTime(request.EndTime, "End time");
            ScheduleHelper.EnsureEndAfterStart(start, end);

            var type = string.IsNullOrWhiteSpace(request.LiveClassesType)
                ? Constants.MaterialAccess.Paid
                : Constants.MaterialAccess.Normalize(request.LiveClassesType);

            var liveClass = new LiveClass
            {
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                LiveClassesType = type,
                YouTubeLink = ParseLink(request.YouTubeLink, "YouTube link"),
                GoogleMeetLink = ParseLink(request.GoogleMeetLink, "Google Meet link"),
                Date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                StartTime = start.ToString(@"hh\:mm"),
                EndTime = end.ToString(@"hh\:mm"),
                CreatedAt = DateTime.UtcNow
            };

            await ApplyCourseAndBatchAsync(liveClass, request.CourseId, request.BatchId);

            // Starts as Not Started unless it was scheduled for a time that is already here or past
            liveClass.Status = Resolve(liveClass);

            return await _repository.CreateAsync(liveClass);
        }

        public async Task<List<LiveClass>> GetAllAsync(bool isAdmin, string userId)
        {
            var all = await _repository.GetAllAsync();
            foreach (var liveClass in all)
            {
                liveClass.Status = Resolve(liveClass);
            }

            var visible = all.Where(l => isAdmin || l.Status != Constants.LiveClassStatuses.Expired).ToList();
            if (!isAdmin)
            {
                var enrollments = await GetVerifiedEnrollmentsAsync(userId);
                visible = visible.Where(l => CanSee(l, enrollments)).ToList();
            }

            return visible
                .OrderBy(l => l.Date, StringComparer.Ordinal)
                .ThenBy(l => l.StartTime, StringComparer.Ordinal)
                .ToList();
        }

        public async Task<LiveClass> GetByIdAsync(string id, bool isAdmin, string userId)
        {
            var liveClass = await _repository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Live class not found");
            liveClass.Status = Resolve(liveClass);

            if (!isAdmin)
            {
                // Same "not found" for expired and for a Paid class the student isn't enrolled in, so ids can't be probed
                if (liveClass.Status == Constants.LiveClassStatuses.Expired ||
                    !CanSee(liveClass, await GetVerifiedEnrollmentsAsync(userId)))
                {
                    throw new KeyNotFoundException("Live class not found");
                }
            }
            return liveClass;
        }

        public async Task<LiveClass> UpdateAsync(string id, LiveClassRequest request)
        {
            var existing = await _repository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Live class not found");

            if (request.Title != null)
            {
                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    throw new InvalidOperationException("Title can't be empty");
                }
                existing.Title = request.Title.Trim();
            }
            if (request.Description != null)
            {
                existing.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            }
            if (request.YouTubeLink != null)
            {
                existing.YouTubeLink = ParseLink(request.YouTubeLink, "YouTube link");
            }
            if (request.GoogleMeetLink != null)
            {
                existing.GoogleMeetLink = ParseLink(request.GoogleMeetLink, "Google Meet link");
            }
            if (!string.IsNullOrWhiteSpace(request.Date))
            {
                existing.Date = ScheduleHelper.ParseDate(request.Date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            if (!string.IsNullOrWhiteSpace(request.StartTime))
            {
                existing.StartTime = ScheduleHelper.ParseTime(request.StartTime, "Start time").ToString(@"hh\:mm");
            }
            if (!string.IsNullOrWhiteSpace(request.EndTime))
            {
                existing.EndTime = ScheduleHelper.ParseTime(request.EndTime, "End time").ToString(@"hh\:mm");
            }
            ScheduleHelper.EnsureEndAfterStart(
                TimeSpan.Parse(existing.StartTime, CultureInfo.InvariantCulture),
                TimeSpan.Parse(existing.EndTime, CultureInfo.InvariantCulture));

            if (!string.IsNullOrWhiteSpace(request.LiveClassesType))
            {
                existing.LiveClassesType = Constants.MaterialAccess.Normalize(request.LiveClassesType);
            }
            // A class saved before liveClassesType existed is Free; make that explicit before the rules below run
            existing.LiveClassesType ??= Constants.MaterialAccess.Free;

            await ApplyCourseAndBatchAsync(existing, request.CourseId, request.BatchId);

            // Rescheduling brings an Expired class back to Not Started / Online automatically
            existing.Status = Resolve(existing);
            existing.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(id, existing);
            return existing;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            _ = await _repository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Live class not found");
            return await _repository.DeleteAsync(id);
        }

        // Applies the course/batch that were sent (null = keep, "" = clear) and then checks the final combination:
        // a batch must belong to the course, and a Paid class needs both (they decide who may see it).
        private async Task ApplyCourseAndBatchAsync(LiveClass liveClass, string? courseId, string? batchId)
        {
            if (courseId != null)
            {
                if (string.IsNullOrWhiteSpace(courseId))
                {
                    liveClass.CourseId = null;
                    liveClass.CourseName = null;
                    // A batch can't outlive its course
                    liveClass.BatchId = null;
                    liveClass.BatchTitle = null;
                }
                else if (courseId != liveClass.CourseId)
                {
                    var course = await _courseRepository.GetByIdAsync(courseId)
                        ?? throw new InvalidOperationException(Constants.Messages.CourseNotFound);
                    liveClass.CourseId = course.Id;
                    liveClass.CourseName = course.CourseName;
                }
            }

            if (batchId != null)
            {
                if (string.IsNullOrWhiteSpace(batchId))
                {
                    liveClass.BatchId = null;
                    liveClass.BatchTitle = null;
                }
                else
                {
                    var batch = await _batchRepository.GetByIdAsync(batchId)
                        ?? throw new InvalidOperationException(Constants.Messages.BatchNotFound);
                    liveClass.BatchId = batch.Id;
                    liveClass.BatchTitle = batch.Title;
                }
            }

            if (!string.IsNullOrWhiteSpace(liveClass.BatchId))
            {
                if (string.IsNullOrWhiteSpace(liveClass.CourseId))
                {
                    throw new InvalidOperationException("Course is required when a batch is selected");
                }
                var batch = await _batchRepository.GetByIdAsync(liveClass.BatchId)
                    ?? throw new InvalidOperationException(Constants.Messages.BatchNotFound);
                if (batch.CourseId != liveClass.CourseId)
                {
                    throw new InvalidOperationException(Constants.Messages.BatchCourseMismatch);
                }
            }

            if (liveClass.LiveClassesType == Constants.MaterialAccess.Paid)
            {
                if (string.IsNullOrWhiteSpace(liveClass.CourseId))
                {
                    throw new InvalidOperationException("Course is required for a Paid live class");
                }
                if (string.IsNullOrWhiteSpace(liveClass.BatchId))
                {
                    throw new InvalidOperationException("Batch is required for a Paid live class");
                }
            }
        }

        private async Task<List<Enrollment>> GetVerifiedEnrollmentsAsync(string userId)
        {
            var enrollments = await _enrollmentRepository.GetByUserIdAsync(userId);
            return enrollments.Where(e => e.Status == Constants.EnrollmentStatuses.Verified).ToList();
        }

        // Free (or saved before the type existed) = everyone; Paid = a verified enrollment in the class's course and batch
        private static bool CanSee(LiveClass liveClass, List<Enrollment> verifiedEnrollments)
        {
            if (liveClass.LiveClassesType != Constants.MaterialAccess.Paid)
            {
                return true;
            }
            return verifiedEnrollments.Any(e =>
                e.CourseId == liveClass.CourseId &&
                (string.IsNullOrWhiteSpace(liveClass.BatchId) || liveClass.BatchId == e.BatchId));
        }

        // Not Started before startTime, Online from startTime until endTime, Expired once endTime is reached
        private string Resolve(LiveClass liveClass)
        {
            var date = DateTime.ParseExact(liveClass.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var start = date + TimeSpan.Parse(liveClass.StartTime, CultureInfo.InvariantCulture);
            var end = date + TimeSpan.Parse(liveClass.EndTime, CultureInfo.InvariantCulture);
            var now = LocalNow();

            if (now >= end)
            {
                return Constants.LiveClassStatuses.Expired;
            }
            return now >= start ? Constants.LiveClassStatuses.Online : Constants.LiveClassStatuses.NotStarted;
        }

        // Blank = no link; otherwise it has to be an http(s) URL
        private static string? ParseLink(string? value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }
            var text = value.Trim();
            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException($"{label} must be a valid link starting with http:// or https://");
            }
            return text;
        }
    }
}

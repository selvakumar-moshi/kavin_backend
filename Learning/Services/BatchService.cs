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

        public BatchService(IBatchRepository batchRepository, ICourseRepository courseRepository)
        {
            _batchRepository = batchRepository;
            _courseRepository = courseRepository;
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

            var batch = new Batch
            {
                Title = request.Title.Trim(),
                CourseId = course.Id,
                CourseName = course.CourseName,
                BatchFrom = request.BatchFrom.Value.Date,
                BatchTo = request.BatchTo.Value.Date,
                CreatedAt = DateTime.UtcNow
            };

            return await _batchRepository.CreateAsync(batch);
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

            await _batchRepository.UpdateAsync(id, batch);
            return batch;
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

            return await _batchRepository.DeleteAsync(id);
        }
    }
}

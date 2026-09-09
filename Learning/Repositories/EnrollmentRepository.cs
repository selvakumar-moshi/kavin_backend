using LearningBackendAPI.Models;
using LearningBackendAPI.Utils;
using MongoDB.Driver;

namespace LearningBackendAPI.Repositories
{
    public class EnrollmentRepository : IEnrollmentRepository
    {
        private readonly IMongoCollection<Enrollment> _enrollments;

        public EnrollmentRepository(IMongoDatabase database)
        {
            _enrollments = database.GetCollection<Enrollment>("Enrollments");
        }

        public async Task<Enrollment> GetByIdAsync(string id)
        {
            return await _enrollments.Find(e => e.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<Enrollment>> GetAllAsync()
        {
            return await _enrollments.Find(_ => true).ToListAsync();
        }

        public async Task<List<Enrollment>> GetByUserIdAsync(string userId)
        {
            return await _enrollments.Find(e => e.UserId == userId).ToListAsync();
        }

        public async Task<Enrollment> CreateAsync(Enrollment enrollment)
        {
            enrollment.CreatedAt = DateTime.UtcNow;
            await _enrollments.InsertOneAsync(enrollment);
            return enrollment;
        }

        public async Task UpdateAsync(string id, Enrollment enrollment)
        {
            enrollment.UpdatedAt = DateTime.UtcNow;
            await _enrollments.ReplaceOneAsync(e => e.Id == id, enrollment);
        }

        public async Task<bool> HasVerifiedEnrollmentAsync(string userId, string courseId)
        {
            var enrollment = await _enrollments
                .Find(e => e.UserId == userId && e.CourseId == courseId && e.Status == Constants.EnrollmentStatuses.Verified)
                .FirstOrDefaultAsync();
            return enrollment != null;
        }

        public async Task<List<string>> GetVerifiedCourseIdsAsync(string userId)
        {
            return await _enrollments
                .Find(e => e.UserId == userId && e.Status == Constants.EnrollmentStatuses.Verified)
                .Project(e => e.CourseId)
                .ToListAsync();
        }
    }
}

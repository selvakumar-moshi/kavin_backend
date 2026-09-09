using LearningBackendAPI.Models;
using MongoDB.Driver;

namespace LearningBackendAPI.Repositories
{
    public class CourseRepository : ICourseRepository
    {
        private readonly IMongoCollection<Course> _courses;

        public CourseRepository(IMongoDatabase database)
        {
            _courses = database.GetCollection<Course>("Courses");
        }

        public async Task<Course> GetByIdAsync(string id)
        {
            return await _courses.Find(c => c.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<Course>> GetAllAsync()
        {
            return await _courses.Find(_ => true).ToListAsync();
        }

        public async Task<long> CountAsync()
        {
            return await _courses.CountDocumentsAsync(_ => true);
        }

        public async Task<Course> CreateAsync(Course course)
        {
            course.CreatedAt = DateTime.UtcNow;
            await _courses.InsertOneAsync(course);
            return course;
        }

        public async Task UpdateAsync(string id, Course course)
        {
            course.UpdatedAt = DateTime.UtcNow;
            await _courses.ReplaceOneAsync(c => c.Id == id, course);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _courses.DeleteOneAsync(c => c.Id == id);
            return result.DeletedCount > 0;
        }

        public async Task<bool> CourseNameExistsAsync(string courseName)
        {
            var course = await _courses.Find(c => c.CourseName == courseName).FirstOrDefaultAsync();
            return course != null;
        }
    }
}
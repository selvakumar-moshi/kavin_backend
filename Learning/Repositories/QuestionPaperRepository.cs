using LearningBackendAPI.Models;
using MongoDB.Driver;

namespace LearningBackendAPI.Repositories
{
    public class QuestionPaperRepository : IQuestionPaperRepository
    {
        private readonly IMongoCollection<QuestionPaper> _questionPapers;

        public QuestionPaperRepository(IMongoDatabase database)
        {
            _questionPapers = database.GetCollection<QuestionPaper>("QuestionPapers");
        }

        public async Task<QuestionPaper> GetByIdAsync(string id)
        {
            return await _questionPapers.Find(s => s.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<QuestionPaper>> GetAllAsync()
        {
            return await _questionPapers.Find(_ => true).ToListAsync();
        }

        public async Task<long> CountAsync()
        {
            return await _questionPapers.CountDocumentsAsync(_ => true);
        }

        public async Task<List<QuestionPaper>> GetByCourseIdAsync(string courseId)
        {
            return await _questionPapers.Find(s => s.CourseId == courseId).ToListAsync();
        }

        public async Task<List<QuestionPaper>> GetByCourseIdsAsync(List<string> courseIds)
        {
            return await _questionPapers.Find(s => courseIds.Contains(s.CourseId)).ToListAsync();
        }

        public async Task<QuestionPaper> CreateAsync(QuestionPaper questionPaper)
        {
            questionPaper.CreatedAt = DateTime.UtcNow;
            await _questionPapers.InsertOneAsync(questionPaper);
            return questionPaper;
        }

        public async Task UpdateAsync(string id, QuestionPaper questionPaper)
        {
            questionPaper.UpdatedAt = DateTime.UtcNow;
            await _questionPapers.ReplaceOneAsync(s => s.Id == id, questionPaper);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _questionPapers.DeleteOneAsync(s => s.Id == id);
            return result.DeletedCount > 0;
        }
    }
}

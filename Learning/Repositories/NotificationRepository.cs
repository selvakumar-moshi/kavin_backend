using LearningBackendAPI.Models;
using MongoDB.Driver;

namespace LearningBackendAPI.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly IMongoCollection<Notification> _notifications;

        public NotificationRepository(IMongoDatabase database)
        {
            _notifications = database.GetCollection<Notification>("Notifications");
        }

        public async Task<Notification> GetByIdAsync(string id)
        {
            return await _notifications.Find(n => n.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<Notification>> GetAllAsync()
        {
            return await _notifications.Find(_ => true)
                .SortByDescending(n => n.Date)
                .ToListAsync();
        }

        public async Task<Notification> CreateAsync(Notification notification)
        {
            notification.CreatedAt = DateTime.UtcNow;
            await _notifications.InsertOneAsync(notification);
            return notification;
        }

        public async Task UpdateAsync(string id, Notification notification)
        {
            notification.UpdatedAt = DateTime.UtcNow;
            await _notifications.ReplaceOneAsync(n => n.Id == id, notification);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _notifications.DeleteOneAsync(n => n.Id == id);
            return result.DeletedCount > 0;
        }
    }
}

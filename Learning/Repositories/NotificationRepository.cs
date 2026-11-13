using LearningBackendAPI.Models;
using LearningBackendAPI.Utils;
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
                .SortByDescending(n => n.StartDate)
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

        public async Task<List<string>> GetDuePushIdsAsync(DateTime now)
            => await _notifications
                .Find(n => n.PushStatus == Constants.PushStatuses.Scheduled && n.PushScheduledAt <= now)
                .Project(n => n.Id)
                .ToListAsync();

        public async Task<Notification?> ClaimForPushAsync(string id, bool onlyIfDue)
        {
            var now = DateTime.UtcNow;
            var f = Builders<Notification>.Filter;
            var filter = f.Eq(n => n.Id, id) & (onlyIfDue
                ? f.Eq(n => n.PushStatus, Constants.PushStatuses.Scheduled) & f.Lte(n => n.PushScheduledAt, now)
                : f.Ne(n => n.PushStatus, Constants.PushStatuses.Sending));
            var update = Builders<Notification>.Update
                .Set(n => n.PushStatus, Constants.PushStatuses.Sending)
                .Set(n => n.PushClaimedAt, now)
                .Set(n => n.PushError, null)
                .Set(n => n.PushTargetCount, 0)
                .Set(n => n.PushSuccessCount, 0)
                .Set(n => n.PushFailureCount, 0);
            return await _notifications.FindOneAndUpdateAsync(filter, update,
                new FindOneAndUpdateOptions<Notification> { ReturnDocument = ReturnDocument.After });
        }

        public async Task SetPushResultAsync(string id, string status, int target, int success, int failure, string? error)
        {
            var update = Builders<Notification>.Update
                .Set(n => n.PushStatus, status)
                .Set(n => n.PushSentAt, DateTime.UtcNow)
                .Set(n => n.PushTargetCount, target)
                .Set(n => n.PushSuccessCount, success)
                .Set(n => n.PushFailureCount, failure)
                .Set(n => n.PushError, error);
            await _notifications.UpdateOneAsync(n => n.Id == id, update);
        }

        public async Task<bool> CancelScheduledPushAsync(string id)
        {
            var result = await _notifications.UpdateOneAsync(
                n => n.Id == id && n.PushStatus == Constants.PushStatuses.Scheduled,
                Builders<Notification>.Update.Set(n => n.PushStatus, Constants.PushStatuses.Cancelled));
            return result.ModifiedCount > 0;
        }

        public async Task<bool> QueuePushNowAsync(string id)
        {
            var update = Builders<Notification>.Update
                .Set(n => n.SendPush, true)
                .Set(n => n.PushStatus, Constants.PushStatuses.Scheduled)
                .Set(n => n.PushScheduledAt, DateTime.UtcNow)
                .Set(n => n.PushError, null);
            var result = await _notifications.UpdateOneAsync(
                n => n.Id == id && n.PushStatus != Constants.PushStatuses.Sending, update);
            return result.ModifiedCount > 0;
        }

        public async Task FailStaleSendingAsync(DateTime claimedBefore)
        {
            var update = Builders<Notification>.Update
                .Set(n => n.PushStatus, Constants.PushStatuses.Failed)
                .Set(n => n.PushError, "The push was interrupted (server restarted while sending). Some devices may have received it; resend if needed.");
            await _notifications.UpdateManyAsync(
                n => n.PushStatus == Constants.PushStatuses.Sending && n.PushClaimedAt < claimedBefore, update);
        }
    }
}

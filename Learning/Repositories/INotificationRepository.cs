using LearningBackendAPI.Models;

namespace LearningBackendAPI.Repositories
{
    public interface INotificationRepository
    {
        Task<Notification> GetByIdAsync(string id);
        Task<List<Notification>> GetAllAsync();
        Task<Notification> CreateAsync(Notification notification);
        Task UpdateAsync(string id, Notification notification);
        Task<bool> DeleteAsync(string id);

        // Push dispatch. These are atomic updates so two instances (or a restart) can't send the same push twice.
        Task<List<string>> GetDuePushIdsAsync(DateTime now);
        /// <summary>Marks the push Sending and returns the notification, or null if another worker got it / it isn't claimable.</summary>
        Task<Notification?> ClaimForPushAsync(string id, bool onlyIfDue);
        Task SetPushResultAsync(string id, string status, int target, int success, int failure, string? error);
        /// <summary>Cancels a Scheduled push; false if it wasn't Scheduled (already sending or sent).</summary>
        Task<bool> CancelScheduledPushAsync(string id);
        /// <summary>Queues a push for now (the scheduler sends it); false if it is already Sending.</summary>
        Task<bool> QueuePushNowAsync(string id);
        /// <summary>A push left in Sending for too long means the server stopped mid-send; mark it Failed so it can be resent.</summary>
        Task FailStaleSendingAsync(DateTime claimedBefore);
    }
}

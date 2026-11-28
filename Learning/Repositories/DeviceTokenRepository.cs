using LearningBackendAPI.Models;
using MongoDB.Driver;

namespace LearningBackendAPI.Repositories
{
    public class DeviceTokenRepository : IDeviceTokenRepository
    {
        private readonly IMongoCollection<DeviceToken> _tokens;

        public DeviceTokenRepository(IMongoDatabase database)
        {
            _tokens = database.GetCollection<DeviceToken>("DeviceTokens");
            // One document per token (and, via UpsertAsync, per user); the userId index serves per-user lookups and cleanup (creating an existing index is a no-op)
            _tokens.Indexes.CreateOne(new CreateIndexModel<DeviceToken>(
                Builders<DeviceToken>.IndexKeys.Ascending(t => t.Token), new CreateIndexOptions { Unique = true }));
            _tokens.Indexes.CreateOne(new CreateIndexModel<DeviceToken>(
                Builders<DeviceToken>.IndexKeys.Ascending(t => t.UserId)));
        }

        // One record per user: the user's record gets the new token (inserted only when the user has none yet).
        public async Task UpsertAsync(string userId, string token, string? platform)
        {
            var now = DateTime.UtcNow;

            // The token index is unique, so free this token if it's still recorded for someone else (a shared phone),
            // and drop the user's other records (older tokens, or duplicates from before this was one-per-user)
            await _tokens.DeleteManyAsync(t => t.Token == token && t.UserId != userId);
            await _tokens.DeleteManyAsync(t => t.UserId == userId && t.Token != token);

            var update = Builders<DeviceToken>.Update
                .Set(t => t.Token, token)
                .Set(t => t.Platform, platform)
                .Set(t => t.LastSeenAt, now)
                .SetOnInsert(t => t.CreatedAt, now);
            await _tokens.UpdateOneAsync(t => t.UserId == userId, update, new UpdateOptions { IsUpsert = true });
        }

        public async Task RemoveAsync(string userId, string token)
            => await _tokens.DeleteOneAsync(t => t.Token == token && t.UserId == userId);

        public async Task RemoveByUserAsync(string userId)
            => await _tokens.DeleteManyAsync(t => t.UserId == userId);

        public async Task RemoveTokensAsync(IEnumerable<string> tokens)
        {
            var list = tokens.ToList();
            if (list.Count > 0)
            {
                await _tokens.DeleteManyAsync(t => list.Contains(t.Token));
            }
        }

        public async Task<(List<DeviceToken> Items, long Total)> GetDevicesAsync(string? userId, int limit)
        {
            var filter = string.IsNullOrWhiteSpace(userId)
                ? Builders<DeviceToken>.Filter.Empty
                : Builders<DeviceToken>.Filter.Eq(t => t.UserId, userId);
            var total = await _tokens.CountDocumentsAsync(filter);
            var items = await _tokens.Find(filter).SortByDescending(t => t.LastSeenAt).Limit(limit).ToListAsync();
            return (items, total);
        }

        public async Task<List<string>> GetTokensAsync(IEnumerable<string>? userIds)
        {
            var filter = userIds == null
                ? Builders<DeviceToken>.Filter.Empty
                : Builders<DeviceToken>.Filter.In(t => t.UserId, userIds);
            return await _tokens.Find(filter).Project(t => t.Token).ToListAsync();
        }
    }
}

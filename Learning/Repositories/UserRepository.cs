using System.Text.RegularExpressions;
using LearningBackendAPI.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LearningBackendAPI.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IMongoCollection<User> _users;

        public UserRepository(IMongoDatabase database)
        {
            _users = database.GetCollection<User>("Users");
        }

        public async Task<User> GetByIdAsync(string id)
        {
            return await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
        }

        public async Task<User> GetByEmailAsync(string email)
        {
            return await _users.Find(u => u.Email == email.ToLower()).FirstOrDefaultAsync();
        }

        public async Task<User> CreateAsync(User user)
        {
            user.Email = user.Email.ToLower();
            user.CreatedAt = DateTime.UtcNow;
            await _users.InsertOneAsync(user);
            return user;
        }

        public async Task UpdateAsync(string id, User user)
        {
            user.UpdatedAt = DateTime.UtcNow;
            await _users.ReplaceOneAsync(u => u.Id == id, user);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _users.DeleteOneAsync(u => u.Id == id);
            return result.DeletedCount > 0;
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            var user = await _users.Find(u => u.Email == email.ToLower()).FirstOrDefaultAsync();
            return user != null;
        }

        public async Task<(List<User> Users, long TotalCount)> GetAllAsync(int pageNumber, int pageSize)
        {
            var filter = Builders<User>.Filter.Empty;
            var totalCount = await _users.CountDocumentsAsync(filter);
            var users = await _users.Find(filter)
                .Skip((pageNumber - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return (users, totalCount);
        }

        public async Task<(List<User> Users, long TotalCount)> SearchAsync(string? searchTerm, Dictionary<string, string>? fieldFilters, int pageNumber, int pageSize,
            DateTime? paymentFrom = null, DateTime? paymentTo = null, DateTime? joinedFrom = null, DateTime? joinedTo = null)
        {
            var conditions = new List<FilterDefinition<User>>();

            AddDateRange(conditions, u => u.PaymentUploadedAt, paymentFrom, paymentTo);
            AddDateRange(conditions, u => u.CreatedAt, joinedFrom, joinedTo);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var pattern = new BsonRegularExpression(Regex.Escape(searchTerm.Trim()), "i");
                conditions.Add(Builders<User>.Filter.Or(
                    Builders<User>.Filter.Regex(u => u.FirstName, pattern),
                    Builders<User>.Filter.Regex(u => u.LastName, pattern),
                    Builders<User>.Filter.Regex(u => u.PhoneNumber, pattern),
                    Builders<User>.Filter.Regex(u => u.Email, pattern),
                    Builders<User>.Filter.Regex(u => u.ApplicationNo, pattern),
                    Builders<User>.Filter.Regex(u => u.District, pattern)
                ));
            }

            if (fieldFilters != null)
            {
                foreach (var (field, value) in fieldFilters)
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    var pattern = new BsonRegularExpression(Regex.Escape(value.Trim()), "i");
                    FilterDefinition<User>? fieldFilter = field.ToLowerInvariant() switch
                    {
                        "firstname" => Builders<User>.Filter.Regex(u => u.FirstName, pattern),
                        "lastname" => Builders<User>.Filter.Regex(u => u.LastName, pattern),
                        "phonenumber" => Builders<User>.Filter.Regex(u => u.PhoneNumber, pattern),
                        "email" => Builders<User>.Filter.Regex(u => u.Email, pattern),
                        "applicationno" => Builders<User>.Filter.Regex(u => u.ApplicationNo, pattern),
                        "district" => Builders<User>.Filter.Regex(u => u.District, pattern),
                        _ => null
                    };

                    if (fieldFilter != null)
                    {
                        conditions.Add(fieldFilter);
                    }
                }
            }

            var filter = conditions.Count == 0
                ? Builders<User>.Filter.Empty
                : Builders<User>.Filter.And(conditions);

            var totalCount = await _users.CountDocumentsAsync(filter);
            var users = await _users.Find(filter)
                .Skip((pageNumber - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return (users, totalCount);
        }

        // Targeted updates (not ReplaceOne) so concurrent logins can't overwrite each other or other profile changes.
        // {savedDeviceId: null} also matches users created before this field existed.
        public async Task<bool> TryRegisterDeviceAsync(string id, string deviceHash)
        {
            var result = await _users.UpdateOneAsync(
                u => u.Id == id && u.SavedDeviceId == null,
                Builders<User>.Update
                    .Set(u => u.SavedDeviceId, deviceHash)
                    .Set(u => u.DeviceRegisteredAt, DateTime.UtcNow));
            return result.ModifiedCount > 0;
        }

        public async Task SetLastLoginAsync(string id, DateTime when)
        {
            await _users.UpdateOneAsync(u => u.Id == id, Builders<User>.Update.Set(u => u.LastLoginDate, when));
        }

        public async Task<bool> ClearDeviceAsync(string id)
        {
            var result = await _users.UpdateOneAsync(
                u => u.Id == id,
                Builders<User>.Update
                    .Set(u => u.SavedDeviceId, null)
                    .Set(u => u.DeviceRegisteredAt, null)
                    .Set(u => u.UpdatedAt, DateTime.UtcNow));
            return result.MatchedCount > 0;
        }

        // from is inclusive; a "to" at midnight is treated as a date and covers that whole day (exclusive upper bound = next midnight)
        private static void AddDateRange(List<FilterDefinition<User>> conditions, System.Linq.Expressions.Expression<Func<User, DateTime?>> field,
            DateTime? from, DateTime? to)
        {
            if (from.HasValue)
            {
                conditions.Add(Builders<User>.Filter.Gte(field, from.Value));
            }
            if (to.HasValue)
            {
                var upper = to.Value.TimeOfDay == TimeSpan.Zero ? to.Value.AddDays(1) : to.Value;
                conditions.Add(to.Value.TimeOfDay == TimeSpan.Zero
                    ? Builders<User>.Filter.Lt(field, upper)
                    : Builders<User>.Filter.Lte(field, upper));
            }
        }

        public async Task<List<User>> GetAllByRoleAsync(string role)
        {
            return await _users.Find(u => u.Role == role).SortBy(u => u.CreatedAt).ToListAsync();
        }

        // Targeted update so it can't overwrite other profile changes
        public async Task SetPaymentUploadedAtAsync(string id, DateTime when)
        {
            await _users.UpdateOneAsync(u => u.Id == id, Builders<User>.Update.Set(u => u.PaymentUploadedAt, when));
        }

        public async Task<long> CountByRoleAsync(string role)
        {
            return await _users.CountDocumentsAsync(u => u.Role == role);
        }

        public async Task<List<User>> GetUsersWithoutApplicationNoAsync()
        {
            // {field: null} matches both an explicit null and a missing field (pre-existing docs).
            var filter = Builders<User>.Filter.Eq(u => u.ApplicationNo, null);
            return await _users.Find(filter).ToListAsync();
        }
    }
}
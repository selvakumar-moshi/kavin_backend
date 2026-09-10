using LearningBackendAPI.Models;
using MongoDB.Driver;

namespace LearningBackendAPI.Repositories
{
    public class CounterRepository : ICounterRepository
    {
        private readonly IMongoCollection<Counter> _counters;

        public CounterRepository(IMongoDatabase database)
        {
            _counters = database.GetCollection<Counter>("Counters");
        }

        public async Task<long> GetNextSequenceAsync(string counterName)
        {
            var filter = Builders<Counter>.Filter.Eq(c => c.Id, counterName);
            var update = Builders<Counter>.Update.Inc(c => c.Seq, 1);
            var options = new FindOneAndUpdateOptions<Counter>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            };

            // Single atomic findAndModify - safe under concurrent registrations, no read-then-write race.
            var counter = await _counters.FindOneAndUpdateAsync(filter, update, options);
            return counter.Seq;
        }
    }
}

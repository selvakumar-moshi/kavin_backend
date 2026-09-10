namespace LearningBackendAPI.Repositories
{
    public interface ICounterRepository
    {
        /// <summary>
        /// Atomically increments and returns the next value (starting at 1) for the named counter.
        /// </summary>
        Task<long> GetNextSequenceAsync(string counterName);
    }
}

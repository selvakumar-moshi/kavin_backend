namespace LearningBackendAPI.Repositories
{
    public interface IDeviceTokenRepository
    {
        /// <summary>Registers the token for the user, moving it from any other user it was registered to.</summary>
        Task UpsertAsync(string userId, string token, string? platform);
        /// <summary>Removes the token, but only if it belongs to that user.</summary>
        Task RemoveAsync(string userId, string token);
        Task RemoveByUserAsync(string userId);
        /// <summary>Removes tokens FCM reported as no longer valid.</summary>
        Task RemoveTokensAsync(IEnumerable<string> tokens);
        /// <summary>Tokens of the given users; every token when userIds is null.</summary>
        Task<List<string>> GetTokensAsync(IEnumerable<string>? userIds);
    }
}

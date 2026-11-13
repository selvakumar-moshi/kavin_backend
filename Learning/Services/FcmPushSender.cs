using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace LearningBackendAPI.Services
{
    public record PushMessage(string? Title, string? Body, string? ImageUrl, Dictionary<string, string> Data);

    public record PushSendResult(int Success, int Failure, List<string> InvalidTokens, string? Error);

    public interface IPushSender
    {
        Task<PushSendResult> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken cancellationToken = default);
    }

    // Sends through Firebase Cloud Messaging. The service account comes from config - either
    // Firebase:CredentialsPath (path to the JSON file) or Firebase:CredentialsJson (the JSON itself, e.g. an
    // environment variable on the server) - and is never committed.
    public class FcmPushSender : IPushSender
    {
        private const int BatchSize = 500; // FCM's limit per multicast
        private readonly IConfiguration _configuration;
        private readonly ILogger<FcmPushSender> _logger;
        private readonly object _initLock = new();
        private FirebaseMessaging? _messaging;

        public FcmPushSender(IConfiguration configuration, ILogger<FcmPushSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private FirebaseMessaging GetMessaging()
        {
            if (_messaging != null)
            {
                return _messaging;
            }

            lock (_initLock)
            {
                if (_messaging != null)
                {
                    return _messaging;
                }

                var app = FirebaseApp.DefaultInstance;
                if (app == null)
                {
                    var path = _configuration["Firebase:CredentialsPath"];
                    var json = _configuration["Firebase:CredentialsJson"];
                    GoogleCredential credential;
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        credential = GoogleCredential.FromJson(json);
                    }
                    else if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        credential = GoogleCredential.FromFile(path);
                    }
                    else
                    {
                        throw new InvalidOperationException("Firebase is not configured: set Firebase:CredentialsPath or Firebase:CredentialsJson to the service account");
                    }
                    app = FirebaseApp.Create(new AppOptions { Credential = credential });
                }

                _messaging = FirebaseMessaging.GetMessaging(app);
                return _messaging;
            }
        }

        public async Task<PushSendResult> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken cancellationToken = default)
        {
            var messaging = GetMessaging();
            int success = 0, failure = 0;
            var invalid = new List<string>();
            string? error = null;

            foreach (var batch in tokens.Chunk(BatchSize))
            {
                var multicast = new MulticastMessage
                {
                    Tokens = batch.ToList(),
                    Notification = new Notification { Title = message.Title, Body = message.Body, ImageUrl = message.ImageUrl },
                    Data = message.Data,
                    Android = new AndroidConfig { Priority = Priority.High }
                };

                try
                {
                    var response = await messaging.SendEachForMulticastAsync(multicast, cancellationToken);
                    success += response.SuccessCount;
                    failure += response.FailureCount;

                    for (var i = 0; i < response.Responses.Count; i++)
                    {
                        var r = response.Responses[i];
                        if (r.IsSuccess)
                        {
                            continue;
                        }
                        // Only Unregistered means the app was uninstalled / the token expired; other errors may be transient
                        if (r.Exception?.MessagingErrorCode == MessagingErrorCode.Unregistered)
                        {
                            invalid.Add(batch[i]);
                        }
                        else
                        {
                            error ??= r.Exception?.Message;
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "FCM batch of {Count} tokens failed", batch.Length);
                    failure += batch.Length;
                    error ??= ex.Message;
                }
            }

            return new PushSendResult(success, failure, invalid, error);
        }
    }
}

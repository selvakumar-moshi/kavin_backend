namespace LearningBackendAPI.Services
{
    // Sends queued and scheduled pushes. Saving a notification with sendPush only queues it; this picks it up within
    // PollInterval, so a request never waits on FCM. Claims are atomic in MongoDB, so a restart or a second instance
    // can't send the same push twice.
    public class PushSchedulerService : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PushSchedulerService> _logger;

        public PushSchedulerService(IServiceScopeFactory scopeFactory, ILogger<PushSchedulerService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<INotificationPushService>().DispatchDueAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Push scheduler tick failed");
                }

                try
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}

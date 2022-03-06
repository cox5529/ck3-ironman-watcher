namespace Ck3IronmanWatcherService
{
    public class Worker : BackgroundService
    {
        private readonly FileWatcherService _watcherService;

        public Worker(FileWatcherService watcherService)
        {
            _watcherService = watcherService;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            FileWatcherService.MakeBackupDirectory();
            _watcherService.StartWatchingSaveGameDirectory();
            return Task.CompletedTask;
        }
    }
}
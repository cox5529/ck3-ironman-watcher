namespace Ck3IronmanWatcherService
{
    public class Worker : BackgroundService
    {
        private readonly FileWatcherService _watcherService;

        public Worker(FileWatcherService watcherService)
        {
            _watcherService = watcherService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _watcherService.MakeBackupDirectory();
            await _watcherService.StartWatchingSaveGameDirectory();
        }
    }
}
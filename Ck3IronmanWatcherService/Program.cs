using Ck3IronmanWatcherService;

var host = Host.CreateDefaultBuilder(args)
               .UseWindowsService(options => { options.ServiceName = "Crusader Kings III Save Watcher"; })
               .ConfigureServices(
                    services =>
                    {
                        services.AddSingleton<FileWatcherService>();
                        services.AddHostedService<Worker>();
                    })
               .Build();

await host.RunAsync();

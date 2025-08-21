

namespace EasyMeals.Crawler;

/// <summary>
///     Worker service that orchestrates the HelloFresh recipe crawling process
///     Runs as a background service and can be scheduled externally (Docker, K8s CronJob, etc.)
/// </summary>
public class Worker(IServiceProvider serviceProvider, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
    }
}
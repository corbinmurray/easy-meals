using EasyMeals.Crawler.Application.Services;

namespace EasyMeals.Crawler;

/// <summary>
///     Worker service that orchestrates the HelloFresh recipe crawling process
///     Runs as a background service and can be scheduled externally (Docker, K8s CronJob, etc.)
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public Worker(IServiceProvider serviceProvider, ILogger<Worker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HelloFresh Crawler Worker starting...");

        try
        {
            // Create a scope for scoped services like DbContext and repositories
            using IServiceScope scope = _serviceProvider.CreateScope();
            var crawlOrchestrationService = scope.ServiceProvider.GetRequiredService<CrawlOrchestrationService>();

            // Run the crawl session once (for external scheduling)
            // If you want continuous crawling, wrap this in a while loop
            await crawlOrchestrationService.StartCrawlSessionAsync(stoppingToken);

            _logger.LogInformation("Crawl session completed successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Crawler was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during crawling");
            throw; // Re-throw to ensure the worker service fails and can be restarted
        }
        finally
        {
            _logger.LogInformation("HelloFresh Crawler Worker stopping...");
        }
    }
}
using EasyMeals.Crawler.Application.Services;

namespace EasyMeals.Crawler;

/// <summary>
///     Worker service that orchestrates the HelloFresh recipe crawling process
///     Runs as a background service and can be scheduled externally (Docker, K8s CronJob, etc.)
/// </summary>
public class Worker(IServiceProvider serviceProvider, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("HelloFresh Crawler Worker starting...");

        try
        {
            // Create a scope for scoped services like DbContext and repositories
            using IServiceScope scope = serviceProvider.CreateScope();
            var crawlOrchestrationService = scope.ServiceProvider.GetRequiredService<CrawlOrchestrationService>();

            // Run the crawl session once (for external scheduling)
            // If you want continuous crawling, wrap this in a while loop
            await crawlOrchestrationService.StartCrawlSessionAsync(stoppingToken);

            logger.LogInformation("Crawl session completed successfully");
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Crawler was cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during crawling");
            throw; // Re-throw to ensure the worker service fails and can be restarted
        }
        finally
        {
            logger.LogInformation("HelloFresh Crawler Worker stopping...");
        }
    }
}
using EasyMeals.Crawler.Application.Services;

namespace EasyMeals.Crawler;

/// <summary>
/// Worker service that orchestrates the HelloFresh recipe crawling process
/// Runs as a background service and can be scheduled externally (Docker, K8s CronJob, etc.)
/// </summary>
public class Worker : BackgroundService
{
    private readonly CrawlOrchestrationService _crawlOrchestrationService;
    private readonly ILogger<Worker> _logger;

    public Worker(CrawlOrchestrationService crawlOrchestrationService, ILogger<Worker> logger)
    {
        _crawlOrchestrationService = crawlOrchestrationService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HelloFresh Crawler Worker starting...");

        try
        {
            // Run the crawl session once (for external scheduling)
            // If you want continuous crawling, wrap this in a while loop
            await _crawlOrchestrationService.StartCrawlSessionAsync(stoppingToken);
            
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
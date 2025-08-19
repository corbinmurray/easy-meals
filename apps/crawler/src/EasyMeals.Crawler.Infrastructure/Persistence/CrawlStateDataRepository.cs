using EasyMeals.Crawler.Domain.Configurations;
using EasyMeals.Crawler.Domain.ValueObjects;
using EasyMeals.Shared.Data.Documents;
using EasyMeals.Shared.Data.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ICrawlStateRepository = EasyMeals.Shared.Data.Repositories.ICrawlStateRepository;

namespace EasyMeals.Crawler.Infrastructure.Persistence;

/// <summary>
/// Source provider agnostic data repository implementation for crawler's crawl state management
/// Bridges between the crawler's domain model and the shared MongoDB infrastructure
/// Follows domain-focused naming conventions while leveraging MongoDB document features
/// Supports multiple source providers through configuration injection
/// </summary>
public class CrawlStateDataRepository(
    ICrawlStateRepository sharedRepository,
    IUnitOfWork unitOfWork,
    IOptions<CrawlerOptions> crawlerOptions,
    ILogger<CrawlStateDataRepository> logger) : EasyMeals.Crawler.Domain.Interfaces.ICrawlStateRepository
{
    private readonly CrawlerOptions _crawlerOptions = crawlerOptions.Value;

    /// <inheritdoc />
    public async Task<CrawlState> LoadStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var stateDocument = await sharedRepository.GetBySourceProviderAsync(_crawlerOptions.SourceProvider, cancellationToken);

            if (stateDocument is null)
            {
                logger.LogInformation("No existing crawl state found for source provider '{SourceProvider}'. Creating new state.",
                    _crawlerOptions.SourceProvider);
                return new CrawlState();
            }

            // Map from MongoDB document to domain value object
            var state = MapFromDocument(stateDocument);

            logger.LogDebug("Loaded crawl state for '{SourceProvider}': {PendingCount} pending, {CompletedCount} completed, {FailedCount} failed",
                _crawlerOptions.SourceProvider, state.PendingUrls.Count(), state.CompletedRecipeIds.Count, state.FailedUrls.Count);

            return state;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading crawl state for source provider '{SourceProvider}' via shared MongoDB infrastructure. Creating new state.",
                _crawlerOptions.SourceProvider);
            return new CrawlState();
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveStateAsync(CrawlState state, CancellationToken cancellationToken = default)
    {
        try
        {
            // Map from domain value object to MongoDB document
            var stateDocument = MapToDocument(state);

            // Use the shared repository's MongoDB-optimized upsert functionality
            var result = await sharedRepository.SaveStateAsync(stateDocument, cancellationToken);

            if (result)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                logger.LogDebug("Successfully saved crawl state for source provider '{SourceProvider}' via shared MongoDB infrastructure",
                    _crawlerOptions.SourceProvider);
            }
            else
            {
                logger.LogWarning("Failed to save crawl state for source provider '{SourceProvider}' via shared MongoDB infrastructure",
                    _crawlerOptions.SourceProvider);
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving crawl state for source provider '{SourceProvider}' via shared MongoDB infrastructure",
                _crawlerOptions.SourceProvider);
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            return false;
        }
    }

    /// <summary>
    /// Maps from crawler domain value object to MongoDB document
    /// Leverages MongoDB's native support for arrays and embedded documents
    /// Follows DDD principles by encapsulating mapping logic
    /// Uses configured source provider for multi-provider support
    /// </summary>
    private CrawlStateDocument MapToDocument(CrawlState state)
    {
        return new CrawlStateDocument
        {
            SourceProvider = _crawlerOptions.SourceProvider,
            PendingUrls = state.PendingUrls.ToList(), // Native MongoDB array
            CompletedRecipeIds = state.CompletedRecipeIds.ToHashSet(), // Use the correct property name
            LastCrawlTime = state.LastCrawlTime,
            Priority = _crawlerOptions.DefaultPriority,
            IsActive = true,
            TotalProcessed = state.TotalProcessed,
            TotalSuccessful = state.TotalSuccessful,
            TotalFailed = state.TotalFailed,
            FailedUrls = state.FailedUrls.Select(url => new FailedUrlDocument
            {
                Url = url,
                ErrorMessage = "Crawl failed",
                FailedAt = DateTime.UtcNow,
                RetryCount = 0
            }).ToList()
        };
    }

    /// <summary>
    /// Maps from MongoDB document to crawler domain value object
    /// Handles MongoDB document structure and supports immutable value object pattern
    /// </summary>
    private static CrawlState MapFromDocument(CrawlStateDocument document)
    {
        // Extract failed URLs from the document structure
        var failedUrls = document.FailedUrls?.Select(f => f.Url).ToHashSet() ?? new HashSet<string>();

        return new CrawlState
        {
            PendingUrls = document.PendingUrls ?? new List<string>(),
            CompletedRecipeIds = document.CompletedRecipeIds ?? new HashSet<string>(),
            FailedUrls = failedUrls,
            LastCrawlTime = document.LastCrawlTime,
            TotalProcessed = document.TotalProcessed,
            TotalSuccessful = document.TotalSuccessful,
            TotalFailed = document.TotalFailed
        };
    }
}

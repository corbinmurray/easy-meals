using EasyMeals.Crawler.Domain.ValueObjects;
using EasyMeals.Shared.Data.Documents;
using EasyMeals.Shared.Data.Repositories;
using Microsoft.Extensions.Logging;
using ICrawlStateRepository = EasyMeals.Shared.Data.Repositories.ICrawlStateRepository;

namespace EasyMeals.Crawler.Infrastructure.Persistence;

/// <summary>
/// MongoDB-optimized data repository implementation for crawler's crawl state management
/// Bridges between the crawler's domain model and the shared MongoDB infrastructure
/// Follows domain-focused naming conventions while leveraging MongoDB document features
/// </summary>
public class CrawlStateDataRepository(
    ICrawlStateRepository sharedRepository,
    IUnitOfWork unitOfWork,
    ILogger<CrawlStateDataRepository> logger) : EasyMeals.Crawler.Domain.Interfaces.ICrawlStateRepository
{
    private const string SourceProvider = "HelloFresh";

    /// <inheritdoc />
    public async Task<CrawlState> LoadStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var stateDocument = await sharedRepository.GetBySourceProviderAsync(SourceProvider, cancellationToken);

            if (stateDocument is null)
            {
                logger.LogInformation("No existing crawl state found. Creating new state.");
                return new CrawlState();
            }

            // Map from MongoDB document to domain value object
            var state = MapFromDocument(stateDocument);

            logger.LogDebug("Loaded crawl state: {PendingCount} pending, {CompletedCount} completed, {FailedCount} failed",
                state.PendingUrls.Count(), state.CompletedRecipeIds.Count, state.FailedUrls.Count);

            return state;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading crawl state via shared MongoDB infrastructure. Creating new state.");
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
                await unitOfWork.CommitAsync(cancellationToken);
                logger.LogDebug("Successfully saved crawl state via shared MongoDB infrastructure");
            }
            else
            {
                logger.LogWarning("Failed to save crawl state via shared MongoDB infrastructure");
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving crawl state via shared MongoDB infrastructure");
            await unitOfWork.RollbackAsync(cancellationToken);
            return false;
        }
    }

    /// <summary>
    /// Maps from crawler domain value object to MongoDB document
    /// Leverages MongoDB's native support for arrays and embedded documents
    /// Follows DDD principles by encapsulating mapping logic
    /// </summary>
    private static CrawlStateDocument MapToDocument(CrawlState state)
    {
        return new CrawlStateDocument
        {
            SourceProvider = SourceProvider,
            PendingUrls = state.PendingUrls.ToList(), // Native MongoDB array
            ProcessedUrls = state.CompletedRecipeIds.ToList(), // Native MongoDB array
            LastCrawlTime = state.LastCrawlTime,
            Priority = 1, // Default priority for HelloFresh
            IsActive = true,
            Metadata = new Dictionary<string, object>
            {
                ["totalProcessed"] = state.TotalProcessed,
                ["totalSuccessful"] = state.TotalSuccessful,
                ["totalFailed"] = state.TotalFailed,
                ["failedUrls"] = state.FailedUrls.ToList()
            }
        };
    }

    /// <summary>
    /// Maps from MongoDB document to crawler domain value object
    /// Handles MongoDB document structure and supports immutable value object pattern
    /// </summary>
    private static CrawlState MapFromDocument(CrawlStateDocument document)
    {
        // Extract metadata with safe defaults
        var metadata = document.Metadata ?? new Dictionary<string, object>();
        var totalProcessed = metadata.TryGetValue("totalProcessed", out var tp) ? Convert.ToInt32(tp) : 0;
        var totalSuccessful = metadata.TryGetValue("totalSuccessful", out var ts) ? Convert.ToInt32(ts) : 0;
        var totalFailed = metadata.TryGetValue("totalFailed", out var tf) ? Convert.ToInt32(tf) : 0;

        var failedUrls = new HashSet<string>();
        if (metadata.TryGetValue("failedUrls", out var fu) && fu is IEnumerable<object> failedList)
        {
            failedUrls = failedList.Select(x => x?.ToString() ?? string.Empty)
                                  .Where(x => !string.IsNullOrEmpty(x))
                                  .ToHashSet();
        }

        return new CrawlState
        {
            PendingUrls = document.PendingUrls ?? new List<string>(),
            CompletedRecipeIds = (document.ProcessedUrls ?? new List<string>()).ToHashSet(),
            FailedUrls = failedUrls,
            LastCrawlTime = document.LastCrawlTime,
            TotalProcessed = totalProcessed,
            TotalSuccessful = totalSuccessful,
            TotalFailed = totalFailed
        };
    }
}

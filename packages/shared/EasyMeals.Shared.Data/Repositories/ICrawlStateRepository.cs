using EasyMeals.Shared.Data.Entities;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
/// Repository interface for CrawlState entities with crawling-specific operations
/// Supports distributed crawling scenarios and state management
/// </summary>
public interface ICrawlStateRepository : IRepository<CrawlStateEntity>
{
    /// <summary>
    /// Gets the crawl state for a specific source provider
    /// Essential for resumable crawling operations
    /// </summary>
    /// <param name="sourceProvider">The source provider name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Crawl state for the provider, or null if not found</returns>
    Task<CrawlStateEntity?> GetBySourceProviderAsync(string sourceProvider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active crawl operations
    /// Supports monitoring and management of distributed crawling
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All currently active crawl states</returns>
    Task<IEnumerable<CrawlStateEntity>> GetActiveStatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets crawl states that haven't been updated within the specified time window
    /// Supports detecting stale or failed crawl operations
    /// </summary>
    /// <param name="olderThan">DateTime threshold for stale detection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stale crawl states requiring attention</returns>
    Task<IEnumerable<CrawlStateEntity>> GetStaleStatesAsync(DateTime olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates or creates a crawl state for a specific provider
    /// Supports upsert operations for state management
    /// </summary>
    /// <param name="state">The crawl state to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the operation succeeded, false otherwise</returns>
    Task<bool> SaveStateAsync(CrawlStateEntity state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a crawl operation as completed and inactive
    /// Supports proper lifecycle management of crawl operations
    /// </summary>
    /// <param name="sourceProvider">The source provider name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the operation succeeded, false otherwise</returns>
    Task<bool> MarkAsCompletedAsync(string sourceProvider, CancellationToken cancellationToken = default);
}

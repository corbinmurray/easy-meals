using EasyMeals.Shared.Data.Documents;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
/// Repository interface for CrawlState documents with crawling-specific operations
/// Supports distributed crawling scenarios and state management
/// Updated for MongoDB document model
/// </summary>
public interface ICrawlStateRepository : IRepository<CrawlStateDocument>
{
    /// <summary>
    /// Gets the crawl state for a specific source provider
    /// Essential for resumable crawling operations
    /// </summary>
    /// <param name="sourceProvider">The source provider name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Crawl state for the provider, or null if not found</returns>
    Task<CrawlStateDocument?> GetBySourceProviderAsync(string sourceProvider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active crawl operations
    /// Supports monitoring and management of distributed crawling
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All currently active crawl states</returns>
    Task<IEnumerable<CrawlStateDocument>> GetActiveStatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets crawl states that haven't been updated within the specified time window
    /// Supports detecting stale or failed crawl operations
    /// </summary>
    /// <param name="olderThan">DateTime threshold for stale detection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stale crawl states requiring attention</returns>
    Task<IEnumerable<CrawlStateDocument>> GetStaleStatesAsync(DateTime olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets crawl states that are ready for scheduled execution
    /// Supports automated crawling based on schedule
    /// </summary>
    /// <param name="currentTime">Current time for schedule comparison</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Crawl states ready for execution</returns>
    Task<IEnumerable<CrawlStateDocument>> GetScheduledStatesAsync(DateTime currentTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates or creates a crawl state for a specific provider
    /// Supports upsert operations for state management
    /// </summary>
    /// <param name="state">The crawl state to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the operation succeeded, false otherwise</returns>
    Task<bool> SaveStateAsync(CrawlStateDocument state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a crawl operation as completed and inactive
    /// Supports proper lifecycle management of crawl operations
    /// </summary>
    /// <param name="sourceProvider">The source provider name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the operation succeeded, false otherwise</returns>
    Task<bool> MarkAsCompletedAsync(string sourceProvider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets crawl states by priority level for prioritized processing
    /// Supports priority-based crawling execution
    /// </summary>
    /// <param name="minPriority">Minimum priority level (1-10)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Crawl states meeting the priority criteria</returns>
    Task<IEnumerable<CrawlStateDocument>> GetByPriorityAsync(int minPriority, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims a crawl state for processing to prevent concurrent execution
    /// Supports distributed locking for crawl operations
    /// </summary>
    /// <param name="sourceProvider">The source provider name</param>
    /// <param name="sessionId">Unique session identifier for the claim</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the claim was successful, false if already claimed</returns>
    Task<bool> TryClaimForProcessingAsync(string sourceProvider, string sessionId, CancellationToken cancellationToken = default);
}

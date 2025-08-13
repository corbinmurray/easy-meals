using EasyMeals.Crawler.Domain.ValueObjects;

namespace EasyMeals.Crawler.Domain.Interfaces;

/// <summary>
/// Interface for managing crawl state persistence
/// </summary>
public interface ICrawlStateRepository
{
    /// <summary>
    /// Loads the current crawl state from persistence
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current crawl state or a new empty state if none exists</returns>
    Task<CrawlState> LoadStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the current crawl state to persistence
    /// </summary>
    /// <param name="state">The crawl state to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if saved successfully, false otherwise</returns>
    Task<bool> SaveStateAsync(CrawlState state, CancellationToken cancellationToken = default);
}

using MongoDB.Driver;
using EasyMeals.Shared.Data.Documents;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
/// MongoDB-specific crawl state repository implementation with optimized queries
/// Provides efficient MongoDB operations for crawl state management
/// Supports distributed crawling scenarios and atomic operations
/// </summary>
public class CrawlStateRepository : MongoRepository<CrawlStateDocument>, ICrawlStateRepository
{
    public CrawlStateRepository(IMongoDatabase database, IClientSessionHandle? session = null)
        : base(database, session)
    {
    }

    /// <summary>
    /// Gets the crawl state for a specific source provider
    /// Uses MongoDB indexed lookup for optimal performance
    /// </summary>
    public async Task<CrawlStateDocument?> GetBySourceProviderAsync(
        string sourceProvider,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<CrawlStateDocument>.Filter.Eq(c => c.SourceProvider, sourceProvider);

        return await _collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets all active crawl operations
    /// Supports monitoring and management of distributed crawling
    /// </summary>
    public async Task<IEnumerable<CrawlStateDocument>> GetActiveStatesAsync(
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<CrawlStateDocument>.Filter.Eq(c => c.IsActive, true);

        return await _collection
            .Find(filter)
            .Sort(Builders<CrawlStateDocument>.Sort.Descending(c => c.Priority).Ascending(c => c.LastCrawlTime))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets crawl states that haven't been updated within the specified time window
    /// Uses MongoDB date comparison for efficient stale detection
    /// </summary>
    public async Task<IEnumerable<CrawlStateDocument>> GetStaleStatesAsync(
        DateTime olderThan,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<CrawlStateDocument>.Filter.And(
            Builders<CrawlStateDocument>.Filter.Eq(c => c.IsActive, true),
            Builders<CrawlStateDocument>.Filter.Lt(c => c.UpdatedAt, olderThan)
        );

        return await _collection
            .Find(filter)
            .Sort(Builders<CrawlStateDocument>.Sort.Ascending(c => c.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets crawl states that are ready for scheduled execution
    /// Supports automated crawling based on schedule
    /// </summary>
    public async Task<IEnumerable<CrawlStateDocument>> GetScheduledStatesAsync(
        DateTime currentTime,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<CrawlStateDocument>.Filter.And(
            Builders<CrawlStateDocument>.Filter.Eq(c => c.IsActive, false),
            Builders<CrawlStateDocument>.Filter.Ne(c => c.NextScheduledCrawl, null),
            Builders<CrawlStateDocument>.Filter.Lte(c => c.NextScheduledCrawl, currentTime),
            Builders<CrawlStateDocument>.Filter.Gt(c => c.PendingUrlCount, 0)
        );

        return await _collection
            .Find(filter)
            .Sort(Builders<CrawlStateDocument>.Sort.Descending(c => c.Priority).Ascending(c => c.NextScheduledCrawl))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Updates or creates a crawl state for a specific provider
    /// Uses MongoDB upsert for efficient state management
    /// </summary>
    public async Task<bool> SaveStateAsync(
        CrawlStateDocument state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            state.MarkAsModified();

            var filter = Builders<CrawlStateDocument>.Filter.Eq(c => c.SourceProvider, state.SourceProvider);
            var options = new ReplaceOptions { IsUpsert = true };

            var result = _session != null
                ? await _collection.ReplaceOneAsync(_session, filter, state, options, cancellationToken)
                : await _collection.ReplaceOneAsync(filter, state, options, cancellationToken);

            return result.IsAcknowledged;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Marks a crawl operation as completed and inactive
    /// Uses MongoDB atomic update for proper lifecycle management
    /// </summary>
    public async Task<bool> MarkAsCompletedAsync(
        string sourceProvider,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = Builders<CrawlStateDocument>.Filter.Eq(c => c.SourceProvider, sourceProvider);
            var update = Builders<CrawlStateDocument>.Update
                .Set(c => c.IsActive, false)
                .Set(c => c.CurrentSessionId, null)
                .Set(c => c.UpdatedAt, DateTime.UtcNow);

            var result = _session != null
                ? await _collection.UpdateOneAsync(_session, filter, update, cancellationToken: cancellationToken)
                : await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets crawl states by priority level for prioritized processing
    /// Uses MongoDB range query for efficient priority-based filtering
    /// </summary>
    public async Task<IEnumerable<CrawlStateDocument>> GetByPriorityAsync(
        int minPriority,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<CrawlStateDocument>.Filter.Gte(c => c.Priority, minPriority);

        return await _collection
            .Find(filter)
            .Sort(Builders<CrawlStateDocument>.Sort.Descending(c => c.Priority).Ascending(c => c.LastCrawlTime))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Atomically claims a crawl state for processing to prevent concurrent execution
    /// Uses MongoDB findAndModify for atomic claim operation
    /// </summary>
    public async Task<bool> TryClaimForProcessingAsync(
        string sourceProvider,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = Builders<CrawlStateDocument>.Filter.And(
                Builders<CrawlStateDocument>.Filter.Eq(c => c.SourceProvider, sourceProvider),
                Builders<CrawlStateDocument>.Filter.Or(
                    Builders<CrawlStateDocument>.Filter.Eq(c => c.IsActive, false),
                    Builders<CrawlStateDocument>.Filter.Eq(c => c.CurrentSessionId, null)
                )
            );

            var update = Builders<CrawlStateDocument>.Update
                .Set(c => c.IsActive, true)
                .Set(c => c.CurrentSessionId, sessionId)
                .Set(c => c.UpdatedAt, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<CrawlStateDocument>
            {
                ReturnDocument = ReturnDocument.After
            };

            var result = _session != null
                ? await _collection.FindOneAndUpdateAsync(_session, filter, update, options, cancellationToken)
                : await _collection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);

            return result != null && result.CurrentSessionId == sessionId;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets crawl states with pending work for processing queue management
    /// Supports efficient work distribution in distributed crawling
    /// </summary>
    public async Task<IEnumerable<CrawlStateDocument>> GetStatesWithPendingWorkAsync(
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<CrawlStateDocument>.Filter.And(
            Builders<CrawlStateDocument>.Filter.Exists(c => c.PendingUrls, true),
            Builders<CrawlStateDocument>.Filter.Ne(c => c.PendingUrls, new List<string>()), // Has at least one pending URL
            Builders<CrawlStateDocument>.Filter.Eq(c => c.IsActive, false)
        );

        return await _collection
            .Find(filter)
            .Sort(Builders<CrawlStateDocument>.Sort.Descending(c => c.Priority).Ascending(c => c.LastCrawlTime))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Updates crawl statistics atomically
    /// Supports efficient metrics tracking without full document replacement
    /// </summary>
    public async Task<bool> UpdateStatisticsAsync(
        string sourceProvider,
        int processedCount,
        int successfulCount,
        int failedCount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = Builders<CrawlStateDocument>.Filter.Eq(c => c.SourceProvider, sourceProvider);
            var update = Builders<CrawlStateDocument>.Update
                .Inc(c => c.TotalProcessed, processedCount)
                .Inc(c => c.TotalSuccessful, successfulCount)
                .Inc(c => c.TotalFailed, failedCount)
                .Set(c => c.LastCrawlTime, DateTime.UtcNow)
                .Set(c => c.UpdatedAt, DateTime.UtcNow);

            var result = _session != null
                ? await _collection.UpdateOneAsync(_session, filter, update, cancellationToken: cancellationToken)
                : await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }
        catch
        {
            return false;
        }
    }
}

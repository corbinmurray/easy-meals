using System.Text.Json;
using EasyMeals.Crawler.Domain.ValueObjects;
using EasyMeals.Shared.Data.Entities;
using EasyMeals.Shared.Data.Repositories;
using Microsoft.Extensions.Logging;
using ICrawlStateRepository = EasyMeals.Shared.Data.Repositories.ICrawlStateRepository;

namespace EasyMeals.Crawler.Infrastructure.Persistence;

/// <summary>
/// Data repository implementation for crawler's crawl state management
/// Bridges between the crawler's domain model and the shared data infrastructure
/// Follows domain-focused naming conventions while maintaining clean architecture
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
            var stateEntity = await sharedRepository.GetBySourceProviderAsync(SourceProvider, cancellationToken);

            if (stateEntity is null)
            {
                logger.LogInformation("No existing crawl state found. Creating new state.");
                return new CrawlState();
            }

            // Map from data entity to domain value object
            var state = MapFromEntity(stateEntity);

            logger.LogDebug("Loaded crawl state: {PendingCount} pending, {CompletedCount} completed, {FailedCount} failed",
                state.PendingUrls.Count(), state.CompletedRecipeIds.Count, state.FailedUrls.Count);

            return state;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading crawl state via shared data infrastructure. Creating new state.");
            return new CrawlState();
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveStateAsync(CrawlState state, CancellationToken cancellationToken = default)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        
        try
        {
            // Map from domain value object to data entity
            var stateEntity = MapToEntity(state);

            // Use the shared repository's upsert functionality
            var result = await sharedRepository.SaveStateAsync(stateEntity, cancellationToken);

            if (result)
                logger.LogDebug("Successfully saved crawl state via shared data infrastructure");
            else
                logger.LogWarning("Failed to save crawl state via shared data infrastructure");

            await unitOfWork.CommitTransactionAsync(cancellationToken);
            
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving crawl state via shared data infrastructure");
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            return false;
        }
    }

    /// <summary>
    /// Maps from crawler domain value object to shared data entity
    /// Follows DDD principles by encapsulating mapping logic
    /// </summary>
    private static CrawlStateEntity MapToEntity(CrawlState state)
    {
        return new CrawlStateEntity
        {
            SourceProvider = SourceProvider,
            PendingUrlsJson = JsonSerializer.Serialize(state.PendingUrls),
            CompletedRecipeIdsJson = JsonSerializer.Serialize(state.CompletedRecipeIds),
            FailedUrlsJson = JsonSerializer.Serialize(state.FailedUrls),
            LastCrawlTime = state.LastCrawlTime,
            TotalProcessed = state.TotalProcessed,
            TotalSuccessful = state.TotalSuccessful,
            TotalFailed = state.TotalFailed
        };
    }

    /// <summary>
    /// Maps from shared data entity to crawler domain value object
    /// Supports immutable value object pattern with proper deserialization
    /// </summary>
    private static CrawlState MapFromEntity(CrawlStateEntity entity)
    {
        var pendingUrls = JsonSerializer.Deserialize<List<string>>(entity.PendingUrlsJson) ?? [];
        var completedIds = JsonSerializer.Deserialize<HashSet<string>>(entity.CompletedRecipeIdsJson) ?? [];
        var failedUrls = JsonSerializer.Deserialize<HashSet<string>>(entity.FailedUrlsJson) ?? [];

        return new CrawlState
        {
            PendingUrls = pendingUrls,
            CompletedRecipeIds = completedIds,
            FailedUrls = failedUrls,
            LastCrawlTime = entity.LastCrawlTime,
            TotalProcessed = entity.TotalProcessed,
            TotalSuccessful = entity.TotalSuccessful,
            TotalFailed = entity.TotalFailed
        };
    }
}

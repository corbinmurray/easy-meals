using System.Text.Json;
using EasyMeals.Crawler.Domain.Interfaces;
using EasyMeals.Crawler.Domain.ValueObjects;
using EasyMeals.Data.Entities;
using EasyMeals.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace EasyMeals.Crawler.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of the crawler's ICrawlStateRepository
/// Bridges between the crawler's domain model and the shared data layer
/// </summary>
public class EfCoreCrawlStateRepositoryAdapter : ICrawlStateRepository
{
    private readonly ICrawlStateDataRepository _dataRepository;
    private readonly ILogger<EfCoreCrawlStateRepositoryAdapter> _logger;
    private const string SourceProvider = "HelloFresh";

    public EfCoreCrawlStateRepositoryAdapter(
        ICrawlStateDataRepository dataRepository, 
        ILogger<EfCoreCrawlStateRepositoryAdapter> logger)
    {
        _dataRepository = dataRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CrawlState> LoadStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var stateEntity = await _dataRepository.LoadStateAsync(SourceProvider, cancellationToken);
            
            if (stateEntity is null)
            {
                _logger.LogInformation("No existing crawl state found. Creating new state.");
                return new CrawlState();
            }

            // Map from data entity to domain value object
            var pendingUrls = JsonSerializer.Deserialize<List<string>>(stateEntity.PendingUrlsJson) ?? new List<string>();
            var completedIds = JsonSerializer.Deserialize<HashSet<string>>(stateEntity.CompletedRecipeIdsJson) ?? new HashSet<string>();
            var failedUrls = JsonSerializer.Deserialize<HashSet<string>>(stateEntity.FailedUrlsJson) ?? new HashSet<string>();

            var state = new CrawlState
            {
                PendingUrls = pendingUrls,
                CompletedRecipeIds = completedIds,
                FailedUrls = failedUrls,
                LastCrawlTime = stateEntity.LastCrawlTime,
                TotalProcessed = stateEntity.TotalProcessed,
                TotalSuccessful = stateEntity.TotalSuccessful,
                TotalFailed = stateEntity.TotalFailed
            };

            _logger.LogDebug("Loaded crawl state: {PendingCount} pending, {CompletedCount} completed, {FailedCount} failed", 
                state.PendingUrls.Count, state.CompletedRecipeIds.Count, state.FailedUrls.Count);

            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading crawl state via shared data layer. Creating new state.");
            return new CrawlState();
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveStateAsync(CrawlState state, CancellationToken cancellationToken = default)
    {
        try
        {
            // Map from domain value object to data entity
            var stateEntity = new CrawlStateEntity
            {
                Id = $"{SourceProvider.ToLowerInvariant()}-state",
                SourceProvider = SourceProvider,
                PendingUrlsJson = JsonSerializer.Serialize(state.PendingUrls),
                CompletedRecipeIdsJson = JsonSerializer.Serialize(state.CompletedRecipeIds),
                FailedUrlsJson = JsonSerializer.Serialize(state.FailedUrls),
                LastCrawlTime = state.LastCrawlTime,
                TotalProcessed = state.TotalProcessed,
                TotalSuccessful = state.TotalSuccessful,
                TotalFailed = state.TotalFailed,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await _dataRepository.SaveStateAsync(stateEntity, cancellationToken);
            
            if (result)
            {
                _logger.LogDebug("Successfully saved crawl state via shared data layer");
            }
            else
            {
                _logger.LogWarning("Failed to save crawl state via shared data layer");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving crawl state via shared data layer");
            return false;
        }
    }
}

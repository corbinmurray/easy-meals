using System.Text.Json;
using EasyMeals.Data.DbContexts;
using EasyMeals.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyMeals.Data.Repositories;

/// <summary>
/// Repository interface for CrawlState entities in the shared data layer
/// </summary>
public interface ICrawlStateDataRepository
{
    Task<CrawlStateEntity?> LoadStateAsync(string sourceProvider, CancellationToken cancellationToken = default);
    Task<bool> SaveStateAsync(CrawlStateEntity state, CancellationToken cancellationToken = default);
    Task<IEnumerable<CrawlStateEntity>> GetAllStatesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// EF Core implementation of ICrawlStateDataRepository
/// Provider-agnostic - works with In-Memory, PostgreSQL, MongoDB, etc.
/// </summary>
public class EfCoreCrawlStateRepository : ICrawlStateDataRepository
{
    private readonly EasyMealsDbContext _context;
    private readonly ILogger<EfCoreCrawlStateRepository> _logger;

    public EfCoreCrawlStateRepository(EasyMealsDbContext context, ILogger<EfCoreCrawlStateRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CrawlStateEntity?> LoadStateAsync(string sourceProvider, CancellationToken cancellationToken = default)
    {
        try
        {
            var state = await _context.CrawlStates
                .FirstOrDefaultAsync(cs => cs.SourceProvider == sourceProvider, cancellationToken);

            if (state is null)
            {
                _logger.LogInformation("No existing crawl state found for provider: {Provider}. Creating new state.", sourceProvider);
                
                // Create a new state for this provider
                state = new CrawlStateEntity
                {
                    Id = $"{sourceProvider.ToLowerInvariant()}-state",
                    SourceProvider = sourceProvider,
                    PendingUrlsJson = "[]",
                    CompletedRecipeIdsJson = "[]",
                    FailedUrlsJson = "[]",
                    LastCrawlTime = DateTime.MinValue,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                await _context.CrawlStates.AddAsync(state, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            _logger.LogDebug("Loaded crawl state for provider: {Provider}", sourceProvider);
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load crawl state for provider: {Provider}", sourceProvider);
            return null;
        }
    }

    public async Task<bool> SaveStateAsync(CrawlStateEntity state, CancellationToken cancellationToken = default)
    {
        try
        {
            var existingState = await _context.CrawlStates
                .FirstOrDefaultAsync(cs => cs.Id == state.Id, cancellationToken);

            if (existingState is not null)
            {
                // Update existing state
                existingState.PendingUrlsJson = state.PendingUrlsJson;
                existingState.CompletedRecipeIdsJson = state.CompletedRecipeIdsJson;
                existingState.FailedUrlsJson = state.FailedUrlsJson;
                existingState.LastCrawlTime = state.LastCrawlTime;
                existingState.TotalProcessed = state.TotalProcessed;
                existingState.TotalSuccessful = state.TotalSuccessful;
                existingState.TotalFailed = state.TotalFailed;
                existingState.UpdatedAt = DateTime.UtcNow;
                
                _context.CrawlStates.Update(existingState);
            }
            else
            {
                // Add new state
                state.CreatedAt = DateTime.UtcNow;
                state.UpdatedAt = DateTime.UtcNow;
                await _context.CrawlStates.AddAsync(state, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogDebug("Saved crawl state for provider: {Provider}", state.SourceProvider);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save crawl state for provider: {Provider}", state.SourceProvider);
            return false;
        }
    }

    public async Task<IEnumerable<CrawlStateEntity>> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.CrawlStates
            .OrderBy(cs => cs.SourceProvider)
            .ToListAsync(cancellationToken);
    }
}

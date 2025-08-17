using EasyMeals.Shared.Data.DbContexts;
using EasyMeals.Shared.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
/// EF Core implementation of ICrawlStateRepository
/// Provides optimized operations for crawl state management and monitoring
/// </summary>
public class CrawlStateRepository(EasyMealsDbContext context) : Repository<CrawlStateEntity>(context), Repository<CrawlStateEntity>, ICrawlStateRepository
{

    /// <inheritdoc />
    public async Task<CrawlStateEntity?> GetBySourceProviderAsync(string sourceProvider, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceProvider))
            throw new ArgumentException("Source provider cannot be null or empty", nameof(sourceProvider));

        return await DbSet
            .FirstOrDefaultAsync(cs => cs.SourceProvider == sourceProvider, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CrawlStateEntity>> GetActiveStatesAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(cs => cs.IsActive)
            .OrderBy(cs => cs.SourceProvider)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CrawlStateEntity>> GetStaleStatesAsync(DateTime olderThan, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(cs => cs.IsActive && cs.UpdatedAt < olderThan)
            .OrderBy(cs => cs.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> SaveStateAsync(CrawlStateEntity state, CancellationToken cancellationToken = default)
    {
        if (state is null)
            throw new ArgumentNullException(nameof(state));

        try
        {
            var existingState = await GetBySourceProviderAsync(state.SourceProvider, cancellationToken);

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
                existingState.IsActive = state.IsActive;
                existingState.MarkAsModified();

                Update(existingState);
            }
            else
            {
                // Add new state
                state.CreatedAt = DateTime.UtcNow;
                state.UpdatedAt = DateTime.UtcNow;
                await AddAsync(state, cancellationToken);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> MarkAsCompletedAsync(string sourceProvider, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceProvider))
            throw new ArgumentException("Source provider cannot be null or empty", nameof(sourceProvider));

        try
        {
            var state = await GetBySourceProviderAsync(sourceProvider, cancellationToken);

            if (state is not null)
            {
                state.IsActive = false;
                state.MarkAsModified();
                Update(state);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}

using EasyMeals.Shared.Data.DbContexts;
using EasyMeals.Shared.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
/// EF Core implementation of IRecipeRepository
/// Provides optimized queries for recipe-specific business operations
/// </summary>
public class RecipeRepository : Repository<RecipeEntity>, IRecipeRepository
{
    public RecipeRepository(EasyMealsDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<PagedResult<RecipeEntity>> GetBySourceProviderAsync(string sourceProvider, int pageNumber, int pageSize, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceProvider))
            throw new ArgumentException("Source provider cannot be null or empty", nameof(sourceProvider));

        var query = DbSet.Where(r => r.SourceProvider == sourceProvider);

        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        // Order by creation date for consistent pagination
        query = query.OrderByDescending(r => r.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<RecipeEntity>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RecipeEntity>> GetByTimeConstraintsAsync(int? maxPrepTime = null, int? maxCookTime = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.Where(r => r.IsActive);

        if (maxPrepTime.HasValue)
        {
            query = query.Where(r => r.PrepTimeMinutes <= maxPrepTime.Value);
        }

        if (maxCookTime.HasValue)
        {
            query = query.Where(r => r.CookTimeMinutes <= maxCookTime.Value);
        }

        // Order by total time for better user experience
        return await query
            .OrderBy(r => r.PrepTimeMinutes + r.CookTimeMinutes)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResult<RecipeEntity>> SearchAsync(string searchTerm, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            throw new ArgumentException("Search term cannot be null or empty", nameof(searchTerm));

        var normalizedSearchTerm = searchTerm.ToLowerInvariant();

        var query = DbSet
            .Where(r => r.IsActive)
            .Where(r => r.Title.ToLower().Contains(normalizedSearchTerm) ||
                       r.Description.ToLower().Contains(normalizedSearchTerm));

        // Order by relevance (title matches first)
        query = query.OrderByDescending(r => r.Title.ToLower().Contains(normalizedSearchTerm))
                    .ThenBy(r => r.Title);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<RecipeEntity>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RecipeEntity>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        if (fromDate > toDate)
            throw new ArgumentException("From date cannot be greater than to date");

        return await DbSet
            .Where(r => r.CreatedAt >= fromDate && r.CreatedAt <= toDate ||
                       r.UpdatedAt >= fromDate && r.UpdatedAt <= toDate)
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsBySourceUrlAsync(string sourceUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            throw new ArgumentException("Source URL cannot be null or empty", nameof(sourceUrl));

        return await DbSet.AnyAsync(r => r.SourceUrl == sourceUrl, cancellationToken);
    }
}

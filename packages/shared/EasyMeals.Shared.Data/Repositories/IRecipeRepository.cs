using EasyMeals.Shared.Data.Entities;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
/// Repository interface for Recipe entities with domain-specific operations
/// Extends the generic repository with recipe-specific business operations
/// </summary>
public interface IRecipeRepository : IRepository<RecipeEntity>
{
    /// <summary>
    /// Gets recipes by source provider with pagination
    /// Essential for multi-provider recipe management
    /// </summary>
    /// <param name="sourceProvider">The source provider name</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="includeInactive">Whether to include inactive recipes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated recipe results</returns>
    Task<PagedResult<RecipeEntity>> GetBySourceProviderAsync(string sourceProvider, int pageNumber, int pageSize, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active recipes with specific cooking time constraints
    /// Supports meal planning business requirements
    /// </summary>
    /// <param name="maxPrepTime">Maximum preparation time in minutes</param>
    /// <param name="maxCookTime">Maximum cooking time in minutes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Recipes matching the time constraints</returns>
    Task<IEnumerable<RecipeEntity>> GetByTimeConstraintsAsync(int? maxPrepTime = null, int? maxCookTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches recipes by title or description
    /// Supports user search functionality
    /// </summary>
    /// <param name="searchTerm">Search term to match against title and description</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated search results</returns>
    Task<PagedResult<RecipeEntity>> SearchAsync(string searchTerm, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recipes created or updated within a specific time range
    /// Supports incremental data processing and synchronization
    /// </summary>
    /// <param name="fromDate">Start date (inclusive)</param>
    /// <param name="toDate">End date (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Recipes within the specified date range</returns>
    Task<IEnumerable<RecipeEntity>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a recipe exists by source URL to prevent duplicates
    /// Essential for crawling operations and data integrity
    /// </summary>
    /// <param name="sourceUrl">The source URL to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if a recipe with the URL exists, false otherwise</returns>
    Task<bool> ExistsBySourceUrlAsync(string sourceUrl, CancellationToken cancellationToken = default);
}

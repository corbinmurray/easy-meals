using EasyMeals.Shared.Data.Documents;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
/// Repository interface for Recipe documents with domain-specific operations
/// Extends the generic repository with recipe-specific business operations
/// Updated for MongoDB document model
/// </summary>
public interface IRecipeRepository : IRepository<RecipeDocument>
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
    Task<PagedResult<RecipeDocument>> GetBySourceProviderAsync(string sourceProvider, int pageNumber, int pageSize, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active recipes with specific cooking time constraints
    /// Supports meal planning business requirements
    /// </summary>
    /// <param name="maxPrepTime">Maximum preparation time in minutes</param>
    /// <param name="maxCookTime">Maximum cooking time in minutes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Recipes matching the time constraints</returns>
    Task<IEnumerable<RecipeDocument>> GetByTimeConstraintsAsync(int? maxPrepTime = null, int? maxCookTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches recipes by title, description, or ingredients using MongoDB text search
    /// Supports comprehensive user search functionality
    /// </summary>
    /// <param name="searchTerm">Search term to match against title, description, and ingredients</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated search results</returns>
    Task<PagedResult<RecipeDocument>> SearchAsync(string searchTerm, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recipes by tags with MongoDB array query optimization
    /// Supports efficient tag-based filtering
    /// </summary>
    /// <param name="tags">Tags to match (any of the specified tags)</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated recipes matching the tags</returns>
    Task<PagedResult<RecipeDocument>> GetByTagsAsync(IEnumerable<string> tags, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recipes by cuisine type with case-insensitive matching
    /// Supports cuisine-based filtering and recommendations
    /// </summary>
    /// <param name="cuisine">Cuisine type to filter by</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated recipes of the specified cuisine</returns>
    Task<PagedResult<RecipeDocument>> GetByCuisineAsync(string cuisine, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recipes created or updated within a specific time range
    /// Supports incremental data processing and synchronization
    /// </summary>
    /// <param name="fromDate">Start date (inclusive)</param>
    /// <param name="toDate">End date (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Recipes within the specified date range</returns>
    Task<IEnumerable<RecipeDocument>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a recipe exists by source URL to prevent duplicates
    /// Essential for crawling operations and data integrity
    /// </summary>
    /// <param name="sourceUrl">The source URL to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if a recipe with the URL exists, false otherwise</returns>
    Task<bool> ExistsBySourceUrlAsync(string sourceUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recipes with nutritional information for health-conscious filtering
    /// Supports nutritional analysis and dietary planning
    /// </summary>
    /// <param name="maxCalories">Maximum calories per serving</param>
    /// <param name="minProtein">Minimum protein in grams</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated recipes meeting nutritional criteria</returns>
    Task<PagedResult<RecipeDocument>> GetByNutritionalCriteriaAsync(int? maxCalories = null, decimal? minProtein = null, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);
}

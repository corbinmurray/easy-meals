using EasyMeals.Shared.Data.Documents;

namespace EasyMeals.Shared.Data.Repositories;

/// <summary>
///     Repository interface for Recipe documents with domain-specific operations
///     Extends the generic repository with recipe-specific business operations
///     Updated for MongoDB document model
/// </summary>
public interface IRecipeRepository : IRepository<RecipeDocument>
{
    /// <summary>
    ///     Searches recipes by title, description, or ingredients using MongoDB text search
    ///     Supports comprehensive user search functionality
    /// </summary>
    /// <param name="searchTerm">Search term to match against title, description, and ingredients</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated search results</returns>
    Task<PagedResult<RecipeDocument>> SearchAsync(string searchTerm, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
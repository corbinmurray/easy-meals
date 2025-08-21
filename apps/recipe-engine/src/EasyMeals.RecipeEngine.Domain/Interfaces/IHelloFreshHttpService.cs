namespace EasyMeals.RecipeEngine.Domain.Interfaces;

/// <summary>
///     Service for interacting with HelloFresh website HTTP endpoints
/// </summary>
public interface IHelloFreshHttpService : IDisposable
{
    /// <summary>
    ///     Fetches HTML content from a HelloFresh recipe URL
    /// </summary>
    /// <param name="recipeUrl">The URL of the recipe to fetch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTML content of the recipe page, or null if failed</returns>
    Task<string?> FetchRecipeHtmlAsync(string recipeUrl, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Discovers recipe URLs from HelloFresh website
    /// </summary>
    /// <param name="maxResults">Maximum number of URLs to discover</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of discovered recipe URLs</returns>
    Task<List<string>> DiscoverRecipeUrlsAsync(int maxResults = 50, CancellationToken cancellationToken = default);
}
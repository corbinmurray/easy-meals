namespace EasyMeals.RecipeEngine.Domain.ValueObjects;

/// <summary>
///     Strategy for discovering recipe URLs from a provider.
/// </summary>
public enum DiscoveryStrategy
{
    /// <summary>
    ///     Static HTML parsing using HtmlAgilityPack
    /// </summary>
    Static,

    /// <summary>
    ///     Dynamic JavaScript rendering using Playwright
    /// </summary>
    Dynamic,

    /// <summary>
    ///     API-based discovery using HTTP client
    /// </summary>
    Api
}
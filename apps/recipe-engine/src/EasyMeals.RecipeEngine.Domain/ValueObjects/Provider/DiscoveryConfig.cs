using System.Text.RegularExpressions;

namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Provider;

/// <summary>
///     Immutable value object representing discovery configuration settings.
/// </summary>
public sealed record DiscoveryConfig
{
    /// <summary>
    ///     Strategy for discovering recipe URLs from the provider.
    /// </summary>
    public DiscoveryStrategy Strategy { get; }

    /// <summary>
    ///     Optional regex pattern to identify recipe URLs for this provider.
    ///     If not provided, discovery service will use default patterns.
    /// </summary>
    public string? RecipeUrlPattern { get; }

    /// <summary>
    ///     Optional regex pattern to identify category/listing URLs for this provider.
    ///     If not provided, discovery service will use default patterns.
    /// </summary>
    public string? CategoryUrlPattern { get; }

    public DiscoveryConfig(
        DiscoveryStrategy strategy,
        string? recipeUrlPattern = null,
        string? categoryUrlPattern = null)
    {
        // Validate regex patterns if provided
        if (!string.IsNullOrWhiteSpace(recipeUrlPattern))
        {
            try
            {
                _ = new Regex(recipeUrlPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"RecipeUrlPattern is not a valid regex: {ex.Message}", nameof(recipeUrlPattern), ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(categoryUrlPattern))
        {
            try
            {
                _ = new Regex(categoryUrlPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"CategoryUrlPattern is not a valid regex: {ex.Message}", nameof(categoryUrlPattern), ex);
            }
        }

        Strategy = strategy;
        RecipeUrlPattern = recipeUrlPattern;
        CategoryUrlPattern = categoryUrlPattern;
    }
}

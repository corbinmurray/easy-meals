namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Provider;

/// <summary>
///     Immutable value object representing provider endpoint configuration.
/// </summary>
public sealed record EndpointInfo
{
    /// <summary>
    ///     Base URL for the recipe provider (must be HTTPS).
    /// </summary>
    public string RecipeRootUrl { get; }

    public EndpointInfo(string recipeRootUrl)
    {
        if (string.IsNullOrWhiteSpace(recipeRootUrl))
            throw new ArgumentException("RecipeRootUrl is required", nameof(recipeRootUrl));

        if (!Uri.IsWellFormedUriString(recipeRootUrl, UriKind.Absolute))
            throw new ArgumentException("RecipeRootUrl must be a valid absolute URL", nameof(recipeRootUrl));

        if (!recipeRootUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("RecipeRootUrl must use HTTPS", nameof(recipeRootUrl));

        RecipeRootUrl = recipeRootUrl;
    }
}

namespace EasyMeals.Domain.ProviderConfiguration;

/// <summary>
/// CSS selectors for extracting recipe properties from HTML.
/// Primary selectors are required; fallback selectors provide resilience.
/// </summary>
/// <remarks>
/// <para>
/// All selector strings are validated via <see cref="CssSelectorValidator"/> using AngleSharp.
/// Invalid selectors will cause validation to fail. Test selectors against sample HTML
/// to verify they extract expected content before persisting configurations.
/// </para>
/// </remarks>
public sealed record ExtractionSelectors
{
    /// <summary>CSS selector for extracting the recipe title.</summary>
    public required string TitleSelector { get; init; }

    /// <summary>Fallback CSS selector for title extraction when primary fails.</summary>
    public string? TitleFallbackSelector { get; init; }

    /// <summary>CSS selector for extracting the recipe description.</summary>
    public required string DescriptionSelector { get; init; }

    /// <summary>Fallback CSS selector for description extraction when primary fails.</summary>
    public string? DescriptionFallbackSelector { get; init; }

    /// <summary>CSS selector for extracting the list of ingredients.</summary>
    public required string IngredientsSelector { get; init; }

    /// <summary>CSS selector for extracting the cooking instructions.</summary>
    public required string InstructionsSelector { get; init; }

    /// <summary>CSS selector for extracting preparation time.</summary>
    public string? PrepTimeSelector { get; init; }

    /// <summary>CSS selector for extracting cooking time.</summary>
    public string? CookTimeSelector { get; init; }

    /// <summary>CSS selector for extracting total time.</summary>
    public string? TotalTimeSelector { get; init; }

    /// <summary>CSS selector for extracting number of servings.</summary>
    public string? ServingsSelector { get; init; }

    /// <summary>CSS selector for extracting the main recipe image URL.</summary>
    public string? ImageUrlSelector { get; init; }

    /// <summary>CSS selector for extracting the recipe author.</summary>
    public string? AuthorSelector { get; init; }

    /// <summary>CSS selector for extracting cuisine type.</summary>
    public string? CuisineSelector { get; init; }

    /// <summary>CSS selector for extracting difficulty level.</summary>
    public string? DifficultySelector { get; init; }

    /// <summary>CSS selector for extracting nutritional information.</summary>
    public string? NutritionSelector { get; init; }

    /// <summary>
    /// Gets all selector values that are non-null for validation purposes.
    /// </summary>
    public IEnumerable<string> GetAllSelectors()
    {
        yield return TitleSelector;
        if (TitleFallbackSelector is not null) yield return TitleFallbackSelector;
        yield return DescriptionSelector;
        if (DescriptionFallbackSelector is not null) yield return DescriptionFallbackSelector;
        yield return IngredientsSelector;
        yield return InstructionsSelector;
        if (PrepTimeSelector is not null) yield return PrepTimeSelector;
        if (CookTimeSelector is not null) yield return CookTimeSelector;
        if (TotalTimeSelector is not null) yield return TotalTimeSelector;
        if (ServingsSelector is not null) yield return ServingsSelector;
        if (ImageUrlSelector is not null) yield return ImageUrlSelector;
        if (AuthorSelector is not null) yield return AuthorSelector;
        if (CuisineSelector is not null) yield return CuisineSelector;
        if (DifficultySelector is not null) yield return DifficultySelector;
        if (NutritionSelector is not null) yield return NutritionSelector;
    }
}

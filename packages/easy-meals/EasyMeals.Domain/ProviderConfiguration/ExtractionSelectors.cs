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
public sealed class ExtractionSelectors
{
    /// <summary>CSS selector for extracting the recipe title.</summary>
    public string TitleSelector { get; private set; }

    /// <summary>Fallback CSS selector for title extraction when primary fails.</summary>
    public string? TitleFallbackSelector { get; private set; }

    /// <summary>CSS selector for extracting the recipe description.</summary>
    public string DescriptionSelector { get; private set; }

    /// <summary>Fallback CSS selector for description extraction when primary fails.</summary>
    public string? DescriptionFallbackSelector { get; private set; }

    /// <summary>CSS selector for extracting the list of ingredients.</summary>
    public string IngredientsSelector { get; private set; }

    /// <summary>CSS selector for extracting the cooking instructions.</summary>
    public string InstructionsSelector { get; private set; }

    /// <summary>CSS selector for extracting preparation time.</summary>
    public string? PrepTimeSelector { get; private set; }

    /// <summary>CSS selector for extracting cooking time.</summary>
    public string? CookTimeSelector { get; private set; }

    /// <summary>CSS selector for extracting total time.</summary>
    public string? TotalTimeSelector { get; private set; }

    /// <summary>CSS selector for extracting number of servings.</summary>
    public string? ServingsSelector { get; private set; }

    /// <summary>CSS selector for extracting the main recipe image URL.</summary>
    public string? ImageUrlSelector { get; private set; }

    /// <summary>CSS selector for extracting the recipe author.</summary>
    public string? AuthorSelector { get; private set; }

    /// <summary>CSS selector for extracting cuisine type.</summary>
    public string? CuisineSelector { get; private set; }

    /// <summary>CSS selector for extracting difficulty level.</summary>
    public string? DifficultySelector { get; private set; }

    /// <summary>CSS selector for extracting nutritional information.</summary>
    public string? NutritionSelector { get; private set; }

    /// <summary>
    /// Creates a new instance of ExtractionSelectors with required selectors.
    /// </summary>
    /// <param name="titleSelector">CSS selector for the recipe title.</param>
    /// <param name="descriptionSelector">CSS selector for the recipe description.</param>
    /// <param name="ingredientsSelector">CSS selector for ingredients list.</param>
    /// <param name="instructionsSelector">CSS selector for cooking instructions.</param>
    /// <param name="titleFallbackSelector">Optional fallback selector for title.</param>
    /// <param name="descriptionFallbackSelector">Optional fallback selector for description.</param>
    /// <param name="prepTimeSelector">Optional selector for prep time.</param>
    /// <param name="cookTimeSelector">Optional selector for cook time.</param>
    /// <param name="totalTimeSelector">Optional selector for total time.</param>
    /// <param name="servingsSelector">Optional selector for servings.</param>
    /// <param name="imageUrlSelector">Optional selector for image URL.</param>
    /// <param name="authorSelector">Optional selector for author.</param>
    /// <param name="cuisineSelector">Optional selector for cuisine type.</param>
    /// <param name="difficultySelector">Optional selector for difficulty level.</param>
    /// <param name="nutritionSelector">Optional selector for nutrition info.</param>
    public ExtractionSelectors(
        string titleSelector,
        string descriptionSelector,
        string ingredientsSelector,
        string instructionsSelector,
        string? titleFallbackSelector = null,
        string? descriptionFallbackSelector = null,
        string? prepTimeSelector = null,
        string? cookTimeSelector = null,
        string? totalTimeSelector = null,
        string? servingsSelector = null,
        string? imageUrlSelector = null,
        string? authorSelector = null,
        string? cuisineSelector = null,
        string? difficultySelector = null,
        string? nutritionSelector = null)
    {
        TitleSelector = titleSelector ?? throw new ArgumentNullException(nameof(titleSelector));
        DescriptionSelector = descriptionSelector ?? throw new ArgumentNullException(nameof(descriptionSelector));
        IngredientsSelector = ingredientsSelector ?? throw new ArgumentNullException(nameof(ingredientsSelector));
        InstructionsSelector = instructionsSelector ?? throw new ArgumentNullException(nameof(instructionsSelector));
        TitleFallbackSelector = titleFallbackSelector;
        DescriptionFallbackSelector = descriptionFallbackSelector;
        PrepTimeSelector = prepTimeSelector;
        CookTimeSelector = cookTimeSelector;
        TotalTimeSelector = totalTimeSelector;
        ServingsSelector = servingsSelector;
        ImageUrlSelector = imageUrlSelector;
        AuthorSelector = authorSelector;
        CuisineSelector = cuisineSelector;
        DifficultySelector = difficultySelector;
        NutritionSelector = nutritionSelector;
    }

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

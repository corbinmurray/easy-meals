namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Recipe;

/// <summary>
///     Value object representing dietary and allergen information for a recipe
///     Immutable value object following DDD principles
/// </summary>
/// <param name="IsVegetarian">Contains no meat or fish</param>
/// <param name="IsVegan">Contains no animal products</param>
/// <param name="IsGlutenFree">Contains no gluten</param>
/// <param name="IsDairyFree">Contains no dairy products</param>
/// <param name="IsNutFree">Contains no tree nuts or peanuts</param>
/// <param name="IsLowCarb">Suitable for low-carbohydrate diets</param>
/// <param name="Allergens">List of allergens present (e.g., "nuts", "shellfish", "eggs")</param>
/// <param name="DietaryTags">Additional dietary classifications (e.g., "Keto", "Paleo", "Whole30")</param>
public sealed record DietaryInfo(
    bool IsVegetarian = false,
    bool IsVegan = false,
    bool IsGlutenFree = false,
    bool IsDairyFree = false,
    bool IsNutFree = false,
    bool IsLowCarb = false,
    IReadOnlyList<string>? Allergens = null,
    IReadOnlyList<string>? DietaryTags = null)
{
    /// <summary>List of allergens present in the recipe</summary>
    public IReadOnlyList<string> Allergens { get; init; } = Allergens ?? [];

    /// <summary>Additional dietary classification tags</summary>
    public IReadOnlyList<string> DietaryTags { get; init; } = DietaryTags ?? [];

    /// <summary>
    ///     Indicates if the recipe has any dietary restrictions marked
    /// </summary>
    public bool HasDietaryInfo =>
        IsVegetarian || IsVegan || IsGlutenFree || IsDairyFree || IsNutFree || IsLowCarb;

    /// <summary>
    ///     Indicates if the recipe contains common allergens
    /// </summary>
    public bool HasAllergens => Allergens.Count > 0;

    /// <summary>
    ///     Gets a summary of dietary attributes for display
    /// </summary>
    public string DietarySummary
    {
        get
        {
            var attributes = new List<string>();

            if (IsVegan) attributes.Add("Vegan");
            else if (IsVegetarian) attributes.Add("Vegetarian");

            if (IsGlutenFree) attributes.Add("Gluten-Free");
            if (IsDairyFree) attributes.Add("Dairy-Free");
            if (IsNutFree) attributes.Add("Nut-Free");
            if (IsLowCarb) attributes.Add("Low-Carb");

            return attributes.Count > 0
                ? string.Join(", ", attributes)
                : "No dietary restrictions";
        }
    }

    /// <summary>
    ///     Checks if the recipe is safe for a person with specific allergen
    /// </summary>
    public bool IsSafeFor(string allergen)
    {
        if (string.IsNullOrWhiteSpace(allergen))
            return true;

        return !Allergens.Any(a =>
            a.Equals(allergen, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Checks if the recipe matches a specific dietary tag
    /// </summary>
    public bool HasDietaryTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        return DietaryTags.Any(t =>
            t.Equals(tag, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Common allergens for reference and validation
    /// </summary>
    public static class CommonAllergens
    {
        public const string Milk = "milk";
        public const string Eggs = "eggs";
        public const string Fish = "fish";
        public const string Shellfish = "shellfish";
        public const string TreeNuts = "tree nuts";
        public const string Peanuts = "peanuts";
        public const string Wheat = "wheat";
        public const string Soybeans = "soybeans";
        public const string Sesame = "sesame";

        public static readonly IReadOnlyList<string> All =
        [
            Milk, Eggs, Fish, Shellfish, TreeNuts, Peanuts, Wheat, Soybeans, Sesame
        ];
    }

    /// <summary>
    ///     Creates a new DietaryInfo with an additional allergen
    /// </summary>
    public DietaryInfo WithAllergen(string allergen)
    {
        if (string.IsNullOrWhiteSpace(allergen))
            return this;

        var normalizedAllergen = allergen.Trim().ToLowerInvariant();

        if (Allergens.Contains(normalizedAllergen))
            return this;

        var newAllergens = Allergens.Append(normalizedAllergen).ToList();
        return this with { Allergens = newAllergens };
    }

    /// <summary>
    ///     Creates a new DietaryInfo with an additional dietary tag
    /// </summary>
    public DietaryInfo WithDietaryTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return this;

        var normalizedTag = tag.Trim();

        if (DietaryTags.Any(t => t.Equals(normalizedTag, StringComparison.OrdinalIgnoreCase)))
            return this;

        var newTags = DietaryTags.Append(normalizedTag).ToList();
        return this with { DietaryTags = newTags };
    }

    /// <summary>
    ///     Factory method for creating empty dietary info
    /// </summary>
    public static DietaryInfo Empty => new();

    /// <summary>
    ///     Factory method for vegan recipes
    /// </summary>
    public static DietaryInfo Vegan => new(
        IsVegetarian: true,
        IsVegan: true,
        IsDairyFree: true);

    /// <summary>
    ///     Factory method for vegetarian recipes
    /// </summary>
    public static DietaryInfo Vegetarian => new(IsVegetarian: true);
}

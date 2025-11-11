namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Recipe;

/// <summary>
///     Ingredient value object representing a recipe ingredient with validation and business rules
///     Immutable value object following DDD principles
/// </summary>
public sealed record Ingredient
{
    /// <summary>
    ///     Creates a new ingredient with validation
    /// </summary>
    /// <param name="name">Name of the ingredient (e.g., "flour", "chicken breast")</param>
    /// <param name="amount">Amount/quantity of the ingredient (e.g., "2", "1.5")</param>
    /// <param name="unit">Unit of measurement (e.g., "cups", "lbs", "tbsp")</param>
    /// <param name="notes">Additional notes or preparation instructions (e.g., "diced", "room temperature")</param>
    /// <param name="isOptional">Whether this ingredient is optional in the recipe</param>
    /// <param name="order">Display order in the ingredient list</param>
    public Ingredient(
        string name,
        string amount,
        string unit,
        string? notes = null,
        bool isOptional = false,
        int order = 0)
    {
        Name = ValidateName(name);
        Amount = ValidateAmount(amount);
        Unit = ValidateUnit(unit);
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        IsOptional = isOptional;
        Order = ValidateOrder(order);
    }

    /// <summary>Name of the ingredient</summary>
    public string Name { get; init; }

    /// <summary>Amount/quantity of the ingredient</summary>
    public string Amount { get; init; }

    /// <summary>Unit of measurement</summary>
    public string Unit { get; init; }

    /// <summary>Additional notes or preparation instructions</summary>
    public string? Notes { get; init; }

    /// <summary>Whether this ingredient is optional in the recipe</summary>
    public bool IsOptional { get; init; }

    /// <summary>Display order in the ingredient list</summary>
    public int Order { get; init; }

    /// <summary>
    ///     Gets formatted display text for the ingredient
    /// </summary>
    public string DisplayText
    {
        get
        {
            string baseText = $"{Amount} {Unit} {Name}".Trim();

            if (!string.IsNullOrEmpty(Notes))
                baseText += $", {Notes}";

            if (IsOptional)
                baseText += " (optional)";

            return baseText;
        }
    }

    /// <summary>
    ///     Gets a simplified display text without notes
    /// </summary>
    public string SimpleDisplayText => $"{Amount} {Unit} {Name}".Trim();

    /// <summary>
    ///     Indicates if the ingredient has preparation notes
    /// </summary>
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    /// <summary>
    ///     Creates a copy of this ingredient with new amount and unit
    /// </summary>
    public Ingredient WithQuantity(string amount, string unit) => this with { Amount = ValidateAmount(amount), Unit = ValidateUnit(unit) };

    /// <summary>
    ///     Creates a copy of this ingredient with new notes
    /// </summary>
    public Ingredient WithNotes(string? notes) => this with { Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim() };

    /// <summary>
    ///     Creates a copy of this ingredient with new optional status
    /// </summary>
    public Ingredient WithOptional(bool isOptional) => this with { IsOptional = isOptional };

    /// <summary>
    ///     Common measurement units for validation and standardization
    /// </summary>
    public static class CommonUnits
    {
        public static readonly string[] VolumeUnits =
        {
            "cup", "cups", "tbsp", "tsp", "fl oz", "ml", "l", "pint", "quart", "gallon"
        };

        public static readonly string[] WeightUnits =
        {
            "oz", "lb", "lbs", "g", "kg", "pound", "pounds"
        };

        public static readonly string[] CountUnits =
        {
            "piece", "pieces", "item", "items", "whole", "clove", "cloves", "slice", "slices"
        };

        /// <summary>
        ///     Gets all common units
        /// </summary>
        public static IEnumerable<string> AllUnits =>
            VolumeUnits.Concat(WeightUnits).Concat(CountUnits);

        /// <summary>
        ///     Checks if a unit is a common/recognized unit
        /// </summary>
        public static bool IsCommonUnit(string unit) =>
            AllUnits.Any(u => string.Equals(u, unit, StringComparison.OrdinalIgnoreCase));
    }

    #region Validation Methods

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Ingredient name cannot be empty", nameof(name));

        if (name.Length > 100)
            throw new ArgumentException("Ingredient name cannot exceed 100 characters", nameof(name));

        return name.Trim();
    }

    private static string ValidateAmount(string amount)
    {
        if (string.IsNullOrWhiteSpace(amount))
            throw new ArgumentException("Ingredient amount cannot be empty", nameof(amount));

        if (amount.Length > 50)
            throw new ArgumentException("Ingredient amount cannot exceed 50 characters", nameof(amount));

        return amount.Trim();
    }

    private static string ValidateUnit(string unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
            throw new ArgumentException("Ingredient unit cannot be empty", nameof(unit));

        if (unit.Length > 20)
            throw new ArgumentException("Ingredient unit cannot exceed 20 characters", nameof(unit));

        return unit.Trim();
    }

    private static int ValidateOrder(int order)
    {
        if (order < 0)
            throw new ArgumentOutOfRangeException(nameof(order), "Ingredient order cannot be negative");

        return order;
    }

    #endregion
}
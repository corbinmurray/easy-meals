namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Recipe;

/// <summary>
///     Nutritional information for a recipe
/// </summary>
/// <param name="Calories">Calories per serving</param>
/// <param name="FatGrams">Total fat in grams per serving</param>
/// <param name="SaturatedFatGrams">Saturated fat in grams per serving</param>
/// <param name="CholesterolMg">Cholesterol in milligrams per serving</param>
/// <param name="SodiumMg">Sodium in milligrams per serving</param>
/// <param name="CarbohydratesGrams">Total carbohydrates in grams per serving</param>
/// <param name="FiberGrams">Dietary fiber in grams per serving</param>
/// <param name="SugarGrams">Sugar in grams per serving</param>
/// <param name="ProteinGrams">Protein in grams per serving</param>
/// <param name="VitaminAPercent">Vitamin A percentage of daily value</param>
/// <param name="VitaminCPercent">Vitamin C percentage of daily value</param>
/// <param name="CalciumPercent">Calcium percentage of daily value</param>
/// <param name="IronPercent">Iron percentage of daily value</param>
/// <param name="AdditionalNutrition">Additional nutritional data as key-value pairs</param>
public record NutritionalInfo(
	int? Calories,
	decimal? FatGrams,
	decimal? SaturatedFatGrams,
	decimal? CholesterolMg,
	decimal? SodiumMg,
	decimal? CarbohydratesGrams,
	decimal? FiberGrams,
	decimal? SugarGrams,
	decimal? ProteinGrams,
	decimal? VitaminAPercent,
	decimal? VitaminCPercent,
	decimal? CalciumPercent,
	decimal? IronPercent,
	Dictionary<string, object>? AdditionalNutrition)
{
	/// <summary>
	///     Indicates if basic nutritional information is available
	/// </summary>
	public bool HasBasicInfo => Calories.HasValue || ProteinGrams.HasValue || CarbohydratesGrams.HasValue || FatGrams.HasValue;

	/// <summary>
	///     Indicates if comprehensive nutritional information is available
	/// </summary>
	public bool IsComprehensive => Calories.HasValue && ProteinGrams.HasValue && CarbohydratesGrams.HasValue && FatGrams.HasValue;

	/// <summary>
	///     Gets formatted calories display text
	/// </summary>
	public string? CaloriesDisplayText => Calories.HasValue ? $"{Calories} cal" : null;

	/// <summary>
	///     Gets formatted protein display text
	/// </summary>
	public string? ProteinDisplayText => ProteinGrams.HasValue ? $"{ProteinGrams:F1}g protein" : null;

	/// <summary>
	///     Gets formatted carbohydrates display text
	/// </summary>
	public string? CarbsDisplayText => CarbohydratesGrams.HasValue ? $"{CarbohydratesGrams:F1}g carbs" : null;

	/// <summary>
	///     Gets formatted fat display text
	/// </summary>
	public string? FatDisplayText => FatGrams.HasValue ? $"{FatGrams:F1}g fat" : null;

	/// <summary>
	///     Gets formatted macronutrient summary
	/// </summary>
	public string MacroSummary
	{
		get
		{
			var parts = new List<string>();
			if (CaloriesDisplayText != null) parts.Add(CaloriesDisplayText);
			if (ProteinDisplayText != null) parts.Add(ProteinDisplayText);
			if (CarbsDisplayText != null) parts.Add(CarbsDisplayText);
			if (FatDisplayText != null) parts.Add(FatDisplayText);
			return string.Join(" • ", parts);
		}
	}
}
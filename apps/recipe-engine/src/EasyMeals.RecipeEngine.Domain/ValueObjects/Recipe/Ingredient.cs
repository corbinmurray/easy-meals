namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Recipe;

/// <summary>
///     Ingredient in a recipe
/// </summary>
/// <param name="Name">Name of the ingredient (e.g., "flour", "chicken breast")</param>
/// <param name="Amount">Amount/quantity of the ingredient (e.g., "2", "1.5")</param>
/// <param name="Unit">Unit of measurement (e.g., "cups", "lbs", "tbsp")</param>
/// <param name="Notes">Additional notes or preparation instructions (e.g., "diced", "room temperature")</param>
/// <param name="IsOptional">Whether this ingredient is optional in the recipe</param>
/// <param name="Order">Display order in the ingredient list</param>
public record Ingredient(
	string Name,
	string Amount,
	string Unit,
	string? Notes,
	bool IsOptional,
	int Order)
{
	public string DisplayText => $"{Amount} {Unit} {Name}".Trim();
}
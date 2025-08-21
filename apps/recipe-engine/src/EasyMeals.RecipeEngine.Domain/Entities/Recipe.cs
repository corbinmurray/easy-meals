using EasyMeals.RecipeEngine.Domain.ValueObjects.Recipe;

namespace EasyMeals.RecipeEngine.Domain.Entities;

/// <summary>
///     Represents a recipe aggregate root containing all recipe data
/// </summary>
public record Recipe(
	string Title,
	string Description,
	IEnumerable<Ingredient> Ingredients,
	IEnumerable<Instruction> Instructions,
	string ImageUrl,
	int PrepTimeMinutes,
	int CookTimeMinutes,
	int Servings,
	NutritionalInfo NutritionalInfo,
	IEnumerable<string> Tags,
	string SourceUrl);
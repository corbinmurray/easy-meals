using EasyMeals.Shared.Data.Repositories.Recipe;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Infrastructure.Repositories;

public class RecipeRepository(
	ILogger<RecipeRepository> logger,
	IRecipeRepository recipeRepository) : Domain.Interfaces.IRecipeRepository
{
}
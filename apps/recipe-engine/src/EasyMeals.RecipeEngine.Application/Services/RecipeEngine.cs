using EasyMeals.RecipeEngine.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Application.Services;

public class RecipeEngine(ILogger<RecipeEngine> logger) : IRecipeEngine
{
	public async Task RunAsync()
	{
	}
}
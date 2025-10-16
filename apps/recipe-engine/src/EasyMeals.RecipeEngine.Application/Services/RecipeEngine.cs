using EasyMeals.RecipeEngine.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Application.Services;

/// <summary>
///     Main recipe engine that orchestrates recipe processing using Saga pattern
///     Demonstrates Clean Architecture and Domain-Driven Design principles
/// </summary>
public class RecipeEngine(ILogger<RecipeEngine> logger) : IRecipeEngine
{
	public async Task RunAsync()
	{
		// Notes: I kind of see this as a point in the process where we
		// get strategies, fire off each strategy pipeline (ie: discovery, fetching, parsing, etc.)
		// and let each task run as necessary -- maybe this needs more research to figure out best practices
		// for potentially long-running background tasks

		logger.LogInformation("Recipe Engine starting at {StartTime}", DateTime.UtcNow);

		try
		{
			// Create a recipe processing saga to orchestrate the workflow
			// var saga = serviceProvider.GetRequiredService<RecipeProcessingSaga>();

			// Start the recipe discovery and processing workflow
			// await saga.StartProcessingAsync(CancellationToken.None);

			logger.LogInformation("Recipe Engine completed successfully");
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Recipe Engine failed with error: {ErrorMessage}", ex.Message);
			throw;
		}
	}
}
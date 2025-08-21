using EasyMeals.RecipeEngine.Domain.Interfaces;

namespace EasyMeals.RecipeEngine;

/// <summary>
///     Worker service that hosts the recipe engine.
/// </summary>
public class Worker(IServiceProvider serviceProvider, ILogger<Worker> logger) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var recipeEngine = serviceProvider.GetRequiredService<IRecipeEngine>();

		logger.LogInformation("Starting the recipe engine {StartTime}", DateTime.UtcNow.ToString("O"));

		try
		{
			await recipeEngine.RunAsync();
		}
		catch (Exception e)
		{
			logger.LogError(e, "Error running the recipe engine. {ErrorMessage} {NewLine} {StackTrace}", 
				e.Message, 
				Environment.NewLine,
				e.StackTrace);
			
			throw;
		}
	}
}
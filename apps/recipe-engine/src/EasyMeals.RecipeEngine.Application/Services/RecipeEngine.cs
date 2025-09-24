using EasyMeals.RecipeEngine.Application.Sagas;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Application.Services;

/// <summary>
///     Main recipe engine that orchestrates recipe processing using Saga pattern
///     Demonstrates Clean Architecture and Domain-Driven Design principles
/// </summary>
public class RecipeEngine(
    ILogger<RecipeEngine> logger,
    IServiceProvider serviceProvider) : IRecipeEngine
{
    public async Task RunAsync()
    {
        logger.LogInformation("Recipe Engine starting at {StartTime}", DateTime.UtcNow);

        try
        {
            // Create a recipe processing saga to orchestrate the workflow
            var saga = serviceProvider.GetRequiredService<RecipeProcessingSaga>();

            // Start the recipe discovery and processing workflow
            await saga.StartProcessingAsync(CancellationToken.None);

            logger.LogInformation("Recipe Engine completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Recipe Engine failed with error: {ErrorMessage}", ex.Message);
            throw;
        }
    }
}
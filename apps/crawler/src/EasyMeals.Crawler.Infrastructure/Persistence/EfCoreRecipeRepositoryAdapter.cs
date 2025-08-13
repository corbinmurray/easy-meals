using EasyMeals.Crawler.Domain.Entities;
using EasyMeals.Crawler.Domain.Interfaces;
using EasyMeals.Data.Mappers;
using EasyMeals.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace EasyMeals.Crawler.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of the crawler's IRecipeRepository
/// Bridges between the crawler's domain model and the shared data layer
/// </summary>
public class EfCoreRecipeRepositoryAdapter : IRecipeRepository
{
    private readonly IRecipeDataRepository _dataRepository;
    private readonly ILogger<EfCoreRecipeRepositoryAdapter> _logger;

    public EfCoreRecipeRepositoryAdapter(
        IRecipeDataRepository dataRepository, 
        ILogger<EfCoreRecipeRepositoryAdapter> logger)
    {
        _dataRepository = dataRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> SaveRecipeAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        try
        {
            // Map from crawler domain model to shared data entity
            var recipeEntity = RecipeMapper.ToEntity(recipe);
            
            // Use the shared data repository
            var result = await _dataRepository.SaveRecipeAsync(recipeEntity, cancellationToken);
            
            if (result)
            {
                _logger.LogDebug("Successfully saved recipe via shared data layer: {Title} ({Id})", recipe.Title, recipe.Id);
            }
            else
            {
                _logger.LogWarning("Failed to save recipe via shared data layer: {Title} ({Id})", recipe.Title, recipe.Id);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving recipe via shared data layer: {Title} ({Id})", recipe.Title, recipe.Id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<int> SaveRecipesAsync(IEnumerable<Recipe> recipes, CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var recipe in recipes)
        {
            var saved = await SaveRecipeAsync(recipe, cancellationToken);
            if (saved) count++;
        }
        
        _logger.LogInformation("Saved {Count} out of {Total} recipes via shared data layer", 
            count, recipes.Count());
        
        return count;
    }

    /// <inheritdoc />
    public async Task<bool> RecipeExistsAsync(string recipeId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dataRepository.RecipeExistsAsync(recipeId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking recipe existence via shared data layer: {RecipeId}", recipeId);
            return false;
        }
    }

    /// <summary>
    /// Gets a recipe by ID (additional method not in the original interface)
    /// </summary>
    public async Task<Recipe?> GetRecipeByIdAsync(string recipeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var recipeEntity = await _dataRepository.GetRecipeByIdAsync(recipeId, cancellationToken);
            if (recipeEntity is null) return null;
            
            // Map from shared data entity back to crawler domain model
            return RecipeMapper.FromEntity<Recipe>(recipeEntity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recipe by ID via shared data layer: {RecipeId}", recipeId);
            return null;
        }
    }
}

using EasyMeals.Crawler.Domain.Entities;
using EasyMeals.Crawler.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EasyMeals.Crawler.Infrastructure.Persistence;

/// <summary>
/// In-memory implementation of IRecipeRepository for testing and initial development
/// This will be replaced with a proper database implementation later
/// </summary>
public class InMemoryRecipeRepository : IRecipeRepository
{
    private readonly List<Recipe> _recipes = new();
    private readonly ILogger<InMemoryRecipeRepository> _logger;

    public InMemoryRecipeRepository(ILogger<InMemoryRecipeRepository> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<bool> SaveRecipeAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(recipe.Id))
            {
                recipe.Id = Guid.NewGuid().ToString();
            }

            // Remove existing recipe with same ID if it exists
            _recipes.RemoveAll(r => r.Id == recipe.Id);
            
            // Add the new/updated recipe
            _recipes.Add(recipe);
            
            _logger.LogDebug("Saved recipe in memory: {Title} ({Id})", recipe.Title, recipe.Id);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save recipe: {Title}", recipe.Title);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task<int> SaveRecipesAsync(IEnumerable<Recipe> recipes, CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var recipe in recipes)
        {
            var saved = SaveRecipeAsync(recipe, cancellationToken).Result;
            if (saved) count++;
        }
        
        return Task.FromResult(count);
    }

    /// <inheritdoc />
    public Task<bool> RecipeExistsAsync(string recipeId, CancellationToken cancellationToken = default)
    {
        var exists = _recipes.Any(r => r.Id == recipeId);
        return Task.FromResult(exists);
    }

    /// <summary>
    /// Gets all stored recipes (for testing/debugging purposes)
    /// </summary>
    public IReadOnlyList<Recipe> GetAllRecipes() => _recipes.AsReadOnly();

    /// <summary>
    /// Gets the count of stored recipes
    /// </summary>
    public int Count => _recipes.Count;
}

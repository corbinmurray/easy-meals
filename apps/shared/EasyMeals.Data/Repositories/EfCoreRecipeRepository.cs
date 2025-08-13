using System.Text.Json;
using EasyMeals.Data.DbContexts;
using EasyMeals.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyMeals.Data.Repositories;

/// <summary>
/// Repository interface for Recipe entities in the shared data layer
/// This will be implemented by EF Core and can be used by both API and Crawler
/// </summary>
public interface IRecipeDataRepository
{
    Task<bool> SaveRecipeAsync(RecipeEntity recipe, CancellationToken cancellationToken = default);
    Task<int> SaveRecipesAsync(IEnumerable<RecipeEntity> recipes, CancellationToken cancellationToken = default);
    Task<bool> RecipeExistsAsync(string recipeId, CancellationToken cancellationToken = default);
    Task<RecipeEntity?> GetRecipeByIdAsync(string recipeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<RecipeEntity>> GetRecipesByProviderAsync(string sourceProvider, CancellationToken cancellationToken = default);
    Task<IEnumerable<RecipeEntity>> GetActiveRecipesAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default);
}

/// <summary>
/// EF Core implementation of IRecipeDataRepository
/// Provider-agnostic - works with In-Memory, PostgreSQL, MongoDB, etc.
/// </summary>
public class EfCoreRecipeRepository : IRecipeDataRepository
{
    private readonly EasyMealsDbContext _context;
    private readonly ILogger<EfCoreRecipeRepository> _logger;

    public EfCoreRecipeRepository(EasyMealsDbContext context, ILogger<EfCoreRecipeRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> SaveRecipeAsync(RecipeEntity recipe, CancellationToken cancellationToken = default)
    {
        try
        {
            var existingRecipe = await _context.Recipes
                .FirstOrDefaultAsync(r => r.Id == recipe.Id, cancellationToken);

            if (existingRecipe is not null)
            {
                // Update existing recipe
                existingRecipe.Title = recipe.Title;
                existingRecipe.Description = recipe.Description;
                existingRecipe.IngredientsJson = recipe.IngredientsJson;
                existingRecipe.InstructionsJson = recipe.InstructionsJson;
                existingRecipe.ImageUrl = recipe.ImageUrl;
                existingRecipe.PrepTimeMinutes = recipe.PrepTimeMinutes;
                existingRecipe.CookTimeMinutes = recipe.CookTimeMinutes;
                existingRecipe.Servings = recipe.Servings;
                existingRecipe.NutritionInfoJson = recipe.NutritionInfoJson;
                existingRecipe.TagsJson = recipe.TagsJson;
                existingRecipe.SourceUrl = recipe.SourceUrl;
                existingRecipe.SourceProvider = recipe.SourceProvider;
                existingRecipe.UpdatedAt = DateTime.UtcNow;
                existingRecipe.IsActive = recipe.IsActive;
                
                _context.Recipes.Update(existingRecipe);
            }
            else
            {
                // Add new recipe
                recipe.CreatedAt = DateTime.UtcNow;
                recipe.UpdatedAt = DateTime.UtcNow;
                await _context.Recipes.AddAsync(recipe, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogDebug("Saved recipe: {Title} ({Id})", recipe.Title, recipe.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save recipe: {Title} ({Id})", recipe.Title, recipe.Id);
            return false;
        }
    }

    public async Task<int> SaveRecipesAsync(IEnumerable<RecipeEntity> recipes, CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var recipe in recipes)
        {
            var saved = await SaveRecipeAsync(recipe, cancellationToken);
            if (saved) count++;
        }
        
        return count;
    }

    public async Task<bool> RecipeExistsAsync(string recipeId, CancellationToken cancellationToken = default)
    {
        return await _context.Recipes
            .AnyAsync(r => r.Id == recipeId, cancellationToken);
    }

    public async Task<RecipeEntity?> GetRecipeByIdAsync(string recipeId, CancellationToken cancellationToken = default)
    {
        return await _context.Recipes
            .FirstOrDefaultAsync(r => r.Id == recipeId, cancellationToken);
    }

    public async Task<IEnumerable<RecipeEntity>> GetRecipesByProviderAsync(string sourceProvider, CancellationToken cancellationToken = default)
    {
        return await _context.Recipes
            .Where(r => r.SourceProvider == sourceProvider && r.IsActive)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<RecipeEntity>> GetActiveRecipesAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        return await _context.Recipes
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}

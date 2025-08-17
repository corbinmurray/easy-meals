using EasyMeals.Crawler.Domain.Entities;
using EasyMeals.Crawler.Domain.Interfaces;
using EasyMeals.Shared.Data.Entities;
using EasyMeals.Shared.Data.Repositories;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EasyMeals.Crawler.Infrastructure.Persistence;

/// <summary>
/// Data repository implementation for crawler's recipe management
/// Bridges between the crawler's domain model and the shared data infrastructure
/// Follows domain-focused naming conventions while maintaining clean architecture
/// </summary>
public class RecipeDataRepository : EasyMeals.Crawler.Domain.Interfaces.IRecipeRepository
{
    private readonly EasyMeals.Shared.Data.Repositories.IRecipeRepository _sharedRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RecipeDataRepository> _logger;

    public RecipeDataRepository(
        EasyMeals.Shared.Data.Repositories.IRecipeRepository sharedRepository,
        IUnitOfWork unitOfWork,
        ILogger<RecipeDataRepository> logger)
    {
        _sharedRepository = sharedRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> SaveRecipeAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        try
        {
            // Map from crawler domain model to shared data entity
            var recipeEntity = MapToEntity(recipe);

            // Use the shared data repository
            await _sharedRepository.AddAsync(recipeEntity, cancellationToken);
            var result = await _unitOfWork.SaveChangesAsync(cancellationToken) > 0;

            if (result)
                _logger.LogDebug("Successfully saved recipe via shared data infrastructure: {Title} ({Id})", recipe.Title, recipe.Id);
            else
                _logger.LogWarning("Failed to save recipe via shared data infrastructure: {Title} ({Id})", recipe.Title, recipe.Id);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving recipe via shared data infrastructure: {Title} ({Id})", recipe.Title, recipe.Id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<int> SaveRecipesAsync(IEnumerable<Recipe> recipes, CancellationToken cancellationToken = default)
    {
        try
        {
            var recipeEntities = recipes.Select(MapToEntity).ToList();

            await _sharedRepository.AddRangeAsync(recipeEntities, cancellationToken);
            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Saved {Count} out of {Total} recipes via shared data infrastructure",
                saved, recipes.Count());

            return saved;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving recipes batch via shared data infrastructure");

            // Fallback to individual saves
            var count = 0;
            foreach (var recipe in recipes)
            {
                var saved = await SaveRecipeAsync(recipe, cancellationToken);
                if (saved) count++;
            }
            return count;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RecipeExistsAsync(string recipeId, CancellationToken cancellationToken = default)
    {
        try
        {
            // For crawler purposes, we'll check by source URL since that's more meaningful
            // The recipeId in crawler context is typically the source URL or a derived identifier
            var exists = await _sharedRepository.ExistsBySourceUrlAsync(recipeId, cancellationToken);
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking recipe existence via shared data infrastructure: {RecipeId}", recipeId);
            return false;
        }
    }

    /// <summary>
    /// Maps from crawler domain model to shared data entity
    /// Follows DDD principles by keeping domain logic separate from infrastructure concerns
    /// </summary>
    private static RecipeEntity MapToEntity(Recipe recipe)
    {
        return new RecipeEntity
        {
            Title = recipe.Title,
            Description = recipe.Description,
            IngredientsJson = JsonSerializer.Serialize(recipe.Ingredients),
            InstructionsJson = JsonSerializer.Serialize(recipe.Instructions),
            ImageUrl = recipe.ImageUrl,
            PrepTimeMinutes = recipe.PrepTimeMinutes,
            CookTimeMinutes = recipe.CookTimeMinutes,
            Servings = recipe.Servings,
            NutritionInfoJson = JsonSerializer.Serialize(recipe.NutritionInfo),
            TagsJson = JsonSerializer.Serialize(recipe.Tags),
            SourceUrl = recipe.SourceUrl,
            SourceProvider = "HelloFresh",
            IsActive = true,
            CreatedAt = recipe.CreatedAt,
            UpdatedAt = recipe.UpdatedAt
        };
    }

    /// <summary>
    /// Maps from shared data entity to crawler domain model
    /// Supports read operations and maintains clean separation of concerns
    /// </summary>
    private static Recipe MapFromEntity(RecipeEntity entity)
    {
        return new Recipe
        {
            Id = entity.Id.ToString(),
            Title = entity.Title,
            Description = entity.Description,
            Ingredients = JsonSerializer.Deserialize<List<string>>(entity.IngredientsJson) ?? [],
            Instructions = JsonSerializer.Deserialize<List<string>>(entity.InstructionsJson) ?? [],
            ImageUrl = entity.ImageUrl,
            PrepTimeMinutes = entity.PrepTimeMinutes,
            CookTimeMinutes = entity.CookTimeMinutes,
            Servings = entity.Servings,
            NutritionInfo = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.NutritionInfoJson) ?? new(),
            Tags = JsonSerializer.Deserialize<List<string>>(entity.TagsJson) ?? [],
            SourceUrl = entity.SourceUrl,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}

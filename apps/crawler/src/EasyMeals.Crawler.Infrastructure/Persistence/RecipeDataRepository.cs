using EasyMeals.Crawler.Domain.Entities;
using EasyMeals.Crawler.Domain.Interfaces;
using EasyMeals.Shared.Data.Documents;
using EasyMeals.Shared.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace EasyMeals.Crawler.Infrastructure.Persistence;

/// <summary>
/// MongoDB-optimized data repository implementation for crawler's recipe management
/// Bridges between the crawler's domain model and the shared MongoDB infrastructure
/// Leverages MongoDB's document structure and embedded document capabilities
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
            // Map from crawler domain model to MongoDB document
            var recipeDocument = MapToDocument(recipe);

            // Use the shared MongoDB repository
            await _sharedRepository.AddAsync(recipeDocument, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogDebug("Successfully saved recipe via shared MongoDB infrastructure: {Title} ({Id})",
                recipe.Title, recipe.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving recipe via shared MongoDB infrastructure: {Title} ({Id})",
                recipe.Title, recipe.Id);
            await _unitOfWork.RollbackAsync(cancellationToken);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<int> SaveRecipesAsync(IEnumerable<Recipe> recipes, CancellationToken cancellationToken = default)
    {
        var recipesList = recipes.ToList();
        if (!recipesList.Any())
            return 0;

        try
        {
            // Map all recipes to MongoDB documents
            var recipeDocuments = recipesList.Select(MapToDocument).ToList();

            // Use MongoDB bulk insert for efficiency
            await _sharedRepository.AddManyAsync(recipeDocuments, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Successfully saved {Count} recipes via shared MongoDB infrastructure",
                recipesList.Count);
            return recipesList.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving recipe batch via shared MongoDB infrastructure. Attempting individual saves.");
            await _unitOfWork.RollbackAsync(cancellationToken);

            // Fallback to individual saves for partial success
            var count = 0;
            foreach (var recipe in recipesList)
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
            _logger.LogError(ex, "Error checking recipe existence via shared MongoDB infrastructure: {RecipeId}",
                recipeId);
            return false;
        }
    }

    /// <summary>
    /// Maps from crawler domain model to MongoDB document
    /// Leverages MongoDB's native support for embedded documents and arrays
    /// Follows DDD principles by keeping domain logic separate from infrastructure concerns
    /// </summary>
    private static RecipeDocument MapToDocument(Recipe recipe)
    {
        return new RecipeDocument
        {
            Title = recipe.Title,
            Description = recipe.Description,
            // Map ingredients to embedded documents (native MongoDB structure)
            Ingredients = recipe.Ingredients?.Select(ingredient => new IngredientDocument
            {
                Name = ingredient, // Simplified mapping - adjust based on your domain model
                Amount = string.Empty, // You may need to parse this from the ingredient string
                Unit = string.Empty
            }).ToList() ?? new List<IngredientDocument>(),
            // Map instructions to embedded documents
            Instructions = recipe.Instructions?.Select((instruction, index) => new InstructionDocument
            {
                StepNumber = index + 1,
                Description = instruction
            }).ToList() ?? new List<InstructionDocument>(),
            ImageUrl = recipe.ImageUrl,
            PrepTimeMinutes = recipe.PrepTimeMinutes,
            CookTimeMinutes = recipe.CookTimeMinutes,
            Servings = recipe.Servings,
            // Map nutrition info to embedded document
            NutritionInfo = recipe.NutritionInfo?.Any() == true ? new NutritionalInfoDocument
            {
                // Map common nutritional fields - adjust based on your data structure
                Calories = TryParseNutritionValue(recipe.NutritionInfo, "calories"),
                ProteinGrams = TryParseNutritionValue(recipe.NutritionInfo, "protein"),
                CarbsGrams = TryParseNutritionValue(recipe.NutritionInfo, "carbs"),
                FatGrams = TryParseNutritionValue(recipe.NutritionInfo, "fat"),
                FiberGrams = TryParseNutritionValue(recipe.NutritionInfo, "fiber"),
                SugarGrams = TryParseNutritionValue(recipe.NutritionInfo, "sugar"),
                SodiumMilligrams = TryParseNutritionValue(recipe.NutritionInfo, "sodium")
            } : null,
            Tags = recipe.Tags ?? new List<string>(), // Native MongoDB array
            SourceUrl = recipe.SourceUrl,
            SourceProvider = "HelloFresh",
            IsActive = true
        };
    }

    /// <summary>
    /// Helper method to safely parse nutritional values from string dictionary
    /// Supports robust data conversion for nutritional information
    /// </summary>
    private static decimal? TryParseNutritionValue(Dictionary<string, string> nutritionInfo, string key)
    {
        if (nutritionInfo?.TryGetValue(key, out var value) == true &&
            decimal.TryParse(value, out var result))
        {
            return result;
        }
        return null;
    }

    /// <summary>
    /// Maps from MongoDB document to crawler domain model
    /// Supports read operations and maintains clean separation of concerns
    /// Note: This method is included for completeness but may not be needed in current crawler workflow
    /// </summary>
    private static Recipe MapFromDocument(RecipeDocument document)
    {
        return new Recipe
        {
            Id = document.Id.ToString(),
            Title = document.Title,
            Description = document.Description,
            // Convert embedded ingredients back to simple strings for domain model compatibility
            Ingredients = document.Ingredients?.Select(i =>
                string.IsNullOrEmpty(i.Amount) ? i.Name : $"{i.Amount} {i.Unit} {i.Name}".Trim())
                .ToList() ?? new List<string>(),
            // Convert embedded instructions back to simple strings
            Instructions = document.Instructions?.OrderBy(i => i.StepNumber)
                .Select(i => i.Description)
                .ToList() ?? new List<string>(),
            ImageUrl = document.ImageUrl,
            PrepTimeMinutes = document.PrepTimeMinutes,
            CookTimeMinutes = document.CookTimeMinutes,
            Servings = document.Servings,
            // Convert embedded nutrition info back to dictionary
            NutritionInfo = document.NutritionInfo != null ? new Dictionary<string, string>
            {
                ["calories"] = document.NutritionInfo.Calories?.ToString() ?? string.Empty,
                ["protein"] = document.NutritionInfo.ProteinGrams?.ToString() ?? string.Empty,
                ["carbs"] = document.NutritionInfo.CarbsGrams?.ToString() ?? string.Empty,
                ["fat"] = document.NutritionInfo.FatGrams?.ToString() ?? string.Empty,
                ["fiber"] = document.NutritionInfo.FiberGrams?.ToString() ?? string.Empty,
                ["sugar"] = document.NutritionInfo.SugarGrams?.ToString() ?? string.Empty,
                ["sodium"] = document.NutritionInfo.SodiumMilligrams?.ToString() ?? string.Empty
            }.Where(kvp => !string.IsNullOrEmpty(kvp.Value)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            : new Dictionary<string, string>(),
            Tags = document.Tags ?? new List<string>(),
            SourceUrl = document.SourceUrl,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt
        };
    }
}

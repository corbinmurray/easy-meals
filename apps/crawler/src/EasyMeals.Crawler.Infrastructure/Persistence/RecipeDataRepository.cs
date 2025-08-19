using EasyMeals.Crawler.Domain.Configurations;
using EasyMeals.Crawler.Domain.Entities;
using EasyMeals.Shared.Data.Documents;
using EasyMeals.Shared.Data.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyMeals.Crawler.Infrastructure.Persistence;

/// <summary>
///     Source provider agnostic data repository implementation for crawler's recipe management
///     Bridges between the crawler's domain model and the shared MongoDB infrastructure
///     Leverages MongoDB's document structure and embedded document capabilities
///     Supports multiple source providers through configuration injection
/// </summary>
public class RecipeDataRepository(
    IRecipeRepository sharedRepository,
    IUnitOfWork unitOfWork,
    IOptions<CrawlerOptions> crawlerOptions,
    ILogger<RecipeDataRepository> logger)
    : Domain.Interfaces.IRecipeRepository
{
    private readonly CrawlerOptions _crawlerOptions = crawlerOptions.Value;

    /// <inheritdoc />
    public async Task<bool> SaveRecipeAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        try
        {
            // Map from crawler domain model to MongoDB document
            RecipeDocument recipeDocument = MapToDocument(recipe);

            // Use the shared MongoDB repository
            await sharedRepository.AddAsync(recipeDocument, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogDebug("Successfully saved recipe via shared MongoDB infrastructure: {Title} ({Id}) from {SourceProvider}",
                recipe.Title, recipe.Id, _crawlerOptions.SourceProvider);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving recipe via shared MongoDB infrastructure: {Title} ({Id}) from {SourceProvider}",
                recipe.Title, recipe.Id, _crawlerOptions.SourceProvider);
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<int> SaveRecipesAsync(IEnumerable<Recipe> recipes, CancellationToken cancellationToken = default)
    {
        List<Recipe> recipesList = recipes.ToList();
        if (!recipesList.Any())
            return 0;

        try
        {
            // Map all recipes to MongoDB documents
            List<RecipeDocument> recipeDocuments = recipesList.Select(MapToDocument).ToList();

            // Use MongoDB bulk insert for efficiency - use AddRangeAsync instead of AddManyAsync
            await sharedRepository.AddRangeAsync(recipeDocuments, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Successfully saved {Count} recipes via shared MongoDB infrastructure from {SourceProvider}",
                recipesList.Count, _crawlerOptions.SourceProvider);
            return recipesList.Count;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving recipe batch via shared MongoDB infrastructure from {SourceProvider}. Attempting individual saves.",
                _crawlerOptions.SourceProvider);
            await unitOfWork.RollbackTransactionAsync(cancellationToken);

            // Fallback to individual saves for partial success
            var count = 0;
            foreach (Recipe recipe in recipesList)
            {
                bool saved = await SaveRecipeAsync(recipe, cancellationToken);
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
            bool exists = await sharedRepository.ExistsBySourceUrlAsync(recipeId, cancellationToken);
            return exists;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking recipe existence via shared MongoDB infrastructure: {RecipeId} from {SourceProvider}",
                recipeId, _crawlerOptions.SourceProvider);
            return false;
        }
    }

    /// <summary>
    ///     Maps from crawler domain model to MongoDB document
    ///     Leverages MongoDB's native support for embedded documents and arrays
    ///     Follows DDD principles by keeping domain logic separate from infrastructure concerns
    ///     Uses configured source provider for multi-provider support
    /// </summary>
    private RecipeDocument MapToDocument(Recipe recipe)
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
            NutritionInfo = recipe.NutritionInfo?.Any() == true
                ? new NutritionalInfoDocument
                {
                    // Map common nutritional fields - adjust based on your data structure
                    Calories = TryParseNutritionValueAsInt(recipe.NutritionInfo, "calories"),
                    ProteinGrams = TryParseNutritionValue(recipe.NutritionInfo, "protein"),
                    CarbohydratesGrams = TryParseNutritionValue(recipe.NutritionInfo, "carbs"),
                    FatGrams = TryParseNutritionValue(recipe.NutritionInfo, "fat"),
                    FiberGrams = TryParseNutritionValue(recipe.NutritionInfo, "fiber"),
                    SugarGrams = TryParseNutritionValue(recipe.NutritionInfo, "sugar"),
                    SodiumMg = TryParseNutritionValue(recipe.NutritionInfo, "sodium")
                }
                : null,
            Tags = recipe.Tags ?? new List<string>(), // Native MongoDB array
            SourceUrl = recipe.SourceUrl,
            SourceProvider = _crawlerOptions.SourceProvider,
            IsActive = true
        };
    }

    /// <summary>
    ///     Helper method to safely parse nutritional values from string dictionary
    ///     Supports robust data conversion for nutritional information
    /// </summary>
    private static decimal? TryParseNutritionValue(Dictionary<string, string> nutritionInfo, string key)
    {
        if (nutritionInfo?.TryGetValue(key, out string? value) == true &&
            decimal.TryParse(value, out decimal result))
            return result;
        return null;
    }

    /// <summary>
    ///     Helper method to safely parse nutritional values as integers (e.g., calories)
    ///     Supports robust data conversion for nutritional information
    /// </summary>
    private static int? TryParseNutritionValueAsInt(Dictionary<string, string> nutritionInfo, string key)
    {
        if (nutritionInfo?.TryGetValue(key, out string? value) == true &&
            int.TryParse(value, out int result))
            return result;
        return null;
    }
}
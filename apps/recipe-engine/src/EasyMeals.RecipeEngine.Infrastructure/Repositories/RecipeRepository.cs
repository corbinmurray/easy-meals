using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Recipe;
using EasyMeals.Shared.Data.Documents.Recipe;
using EasyMeals.Shared.Data.Repositories;
using MongoDB.Driver;

namespace EasyMeals.RecipeEngine.Infrastructure.Repositories;

public class RecipeRepository(IMongoDatabase database, IClientSessionHandle? session = null)
    : MongoRepository<RecipeDocument>(database, session), IRecipeRepository
{
    public async Task<Recipe?> GetByIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
    {
        RecipeDocument? document = await base.GetByIdAsync(recipeId.ToString(), cancellationToken);
        return document == null ? null : ToDomain(document);
    }

    public async Task<Recipe?> GetByUrlAsync(string recipeUrl, string providerId, CancellationToken cancellationToken = default)
    {
        RecipeDocument? document = await GetFirstOrDefaultAsync(
            d => d.SourceUrl == recipeUrl && d.SourceProvider == providerId,
            cancellationToken);

        return document == null ? null : ToDomain(document);
    }

    public async Task SaveAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        RecipeDocument document = ToDocument(recipe);
        await ReplaceOneAsync(d => d.Id == recipe.Id.ToString(), document, cancellationToken: cancellationToken);
    }

    public async Task SaveBatchAsync(IReadOnlyList<Recipe> recipes, CancellationToken cancellationToken = default)
    {
        if (recipes.Count == 0)
            return;

        List<RecipeDocument> documents = recipes.Select(ToDocument).ToList();
        await InsertManyAsync(documents, cancellationToken);
    }

    public async Task<int> CountByProviderAsync(string providerId, CancellationToken cancellationToken = default)
    {
        long count = await CountAsync(d => d.SourceProvider == providerId, cancellationToken);
        return (int)count;
    }

    private static Recipe ToDomain(RecipeDocument document)
    {
        // Convert embedded ingredient documents to value objects
        List<Ingredient> ingredients = document.Ingredients
            .Select(i => new Ingredient(
                i.Name,
                i.Amount,
                i.Unit,
                i.Notes,
                i.IsOptional,
                i.Order))
            .ToList();

        // Convert embedded instruction documents to value objects
        List<Instruction> instructions = document.Instructions
            .Select(i => new Instruction(
                i.StepNumber,
                i.Description,
                i.TimeMinutes,
                i.Temperature,
                i.Equipment,
                i.MediaUrl,
                i.Tips))
            .ToList();

        // Convert nutritional info if present
        NutritionalInfo? nutritionalInfo = document.NutritionInfo == null
            ? null
            : new NutritionalInfo(
                document.NutritionInfo.Calories,
                document.NutritionInfo.FatGrams,
                document.NutritionInfo.SaturatedFatGrams,
                document.NutritionInfo.CholesterolMg,
                document.NutritionInfo.SodiumMg,
                document.NutritionInfo.CarbohydratesGrams,
                document.NutritionInfo.FiberGrams,
                document.NutritionInfo.SugarGrams,
                document.NutritionInfo.ProteinGrams,
                document.NutritionInfo.VitaminAPercent,
                document.NutritionInfo.VitaminCPercent,
                document.NutritionInfo.CalciumPercent,
                document.NutritionInfo.IronPercent,
                document.NutritionInfo.AdditionalNutrition);

        // Reconstitute the Recipe aggregate root
        Recipe recipe = Recipe.Reconstitute(
            Guid.Parse(document.Id),
            document.Title,
            document.Description,
            ingredients,
            instructions,
            document.ImageUrl,
            document.PrepTimeMinutes,
            document.CookTimeMinutes,
            document.Servings,
            nutritionalInfo,
            document.Tags,
            document.SourceUrl,
            document.SourceProvider,
            document.IsActive,
            document.Cuisine,
            document.Difficulty,
            document.Rating,
            document.ReviewCount,
            document.CreatedAt,
            document.UpdatedAt);

        return recipe;
    }

    private static RecipeDocument ToDocument(Recipe recipe)
    {
        return new RecipeDocument
        {
            Id = recipe.Id.ToString(),
            Title = recipe.Title,
            Description = recipe.Description,
            Ingredients = recipe.Ingredients
                .Select(i => new IngredientDocument
                {
                    Name = i.Name,
                    Amount = i.Amount,
                    Unit = i.Unit,
                    Notes = i.Notes,
                    IsOptional = i.IsOptional,
                    Order = i.Order
                })
                .ToList(),
            Instructions = recipe.Instructions
                .Select(i => new InstructionDocument
                {
                    StepNumber = i.StepNumber,
                    Description = i.Description,
                    TimeMinutes = i.TimeMinutes,
                    Temperature = i.Temperature,
                    Equipment = i.Equipment,
                    MediaUrl = i.MediaUrl,
                    Tips = i.Tips
                })
                .ToList(),
            ImageUrl = recipe.ImageUrl,
            PrepTimeMinutes = recipe.PrepTimeMinutes,
            CookTimeMinutes = recipe.CookTimeMinutes,
            Servings = recipe.Servings,
            NutritionInfo = recipe.NutritionalInfo == null
                ? null
                : new NutritionalInfoDocument
                {
                    Calories = recipe.NutritionalInfo.Calories,
                    FatGrams = recipe.NutritionalInfo.FatGrams,
                    SaturatedFatGrams = recipe.NutritionalInfo.SaturatedFatGrams,
                    CholesterolMg = recipe.NutritionalInfo.CholesterolMg,
                    SodiumMg = recipe.NutritionalInfo.SodiumMg,
                    CarbohydratesGrams = recipe.NutritionalInfo.CarbohydratesGrams,
                    FiberGrams = recipe.NutritionalInfo.FiberGrams,
                    SugarGrams = recipe.NutritionalInfo.SugarGrams,
                    ProteinGrams = recipe.NutritionalInfo.ProteinGrams,
                    VitaminAPercent = recipe.NutritionalInfo.VitaminAPercent,
                    VitaminCPercent = recipe.NutritionalInfo.VitaminCPercent,
                    CalciumPercent = recipe.NutritionalInfo.CalciumPercent,
                    IronPercent = recipe.NutritionalInfo.IronPercent,
                    AdditionalNutrition = recipe.NutritionalInfo.AdditionalNutrition
                },
            Tags = recipe.Tags.ToList(),
            SourceUrl = recipe.SourceUrl,
            SourceProvider = recipe.SourceProvider,
            IsActive = recipe.IsActive,
            Cuisine = recipe.Cuisine,
            Difficulty = recipe.Difficulty,
            Rating = recipe.Rating,
            ReviewCount = recipe.ReviewCount,
            CreatedAt = recipe.CreatedAt,
            UpdatedAt = recipe.UpdatedAt
        };
    }
}
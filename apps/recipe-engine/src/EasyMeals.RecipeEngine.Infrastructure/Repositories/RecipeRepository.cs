using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Recipe;
using EasyMeals.Shared.Data.Documents.Recipe;
using EasyMeals.Shared.Data.Repositories;
using MongoDB.Driver;

namespace EasyMeals.RecipeEngine.Infrastructure.Repositories;

public class RecipeRepository : MongoRepository<RecipeDocument>, Domain.Interfaces.IRecipeRepository
{
	public RecipeRepository(IMongoDatabase database, IClientSessionHandle? session = null)
		: base(database, session)
	{
	}

	public async Task<Recipe?> GetByIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
	{
		RecipeDocument? document = await base.GetByIdAsync(recipeId.ToString(), cancellationToken);
		return document == null ? null : ToDomain(document);
	}

	public async Task<Recipe?> GetByUrlAsync(string recipeUrl, string providerId, CancellationToken cancellationToken = default)
	{
		RecipeDocument? document = await base.GetFirstOrDefaultAsync(
			d => d.SourceUrl == recipeUrl && d.SourceProvider == providerId,
			cancellationToken);

		return document == null ? null : ToDomain(document);
	}

	public async Task SaveAsync(Recipe recipe, CancellationToken cancellationToken = default)
	{
		RecipeDocument document = ToDocument(recipe);
		await base.ReplaceOneAsync(d => d.Id == recipe.Id.ToString(), document, cancellationToken: cancellationToken);
	}

	public async Task SaveBatchAsync(IReadOnlyList<Recipe> recipes, CancellationToken cancellationToken = default)
	{
		if (recipes.Count == 0)
			return;

		List<RecipeDocument> documents = recipes.Select(ToDocument).ToList();
		await base.InsertManyAsync(documents, cancellationToken);
	}

	public async Task<int> CountByProviderAsync(string providerId, CancellationToken cancellationToken = default)
	{
		long count = await base.CountAsync(d => d.SourceProvider == providerId, cancellationToken);
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
				i.Notes ?? string.Empty))
			.ToList();

		// Convert embedded instruction documents to value objects
		List<Instruction> instructions = document.Instructions
			.Select(i => new Instruction(
				i.StepNumber,
				i.Description,
				i.TimeMinutes))
			.ToList();

		// Convert nutritional info if present
		NutritionalInfo? nutritionalInfo = document.NutritionInfo == null
			? null
			: new NutritionalInfo(
				document.NutritionInfo.Calories,
				document.NutritionInfo.ProteinGrams,
				document.NutritionInfo.CarbohydratesGrams,
				document.NutritionInfo.FatGrams,
				document.NutritionInfo.FiberGrams,
				document.NutritionInfo.SugarGrams);

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
			Id = recipe.Id,
			Title = recipe.Title,
			Description = recipe.Description,
			Ingredients = recipe.Ingredients
				.Select(i => new IngredientDocument
				{
					Name = i.Name,
					Quantity = i.Quantity,
					Unit = i.Unit,
					Notes = i.Notes
				})
				.ToList(),
			Instructions = recipe.Instructions
				.Select(i => new InstructionDocument
				{
					StepNumber = i.StepNumber,
					Description = i.Description,
					TimingMinutes = i.TimingMinutes
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
					Protein = recipe.NutritionalInfo.Protein,
					Carbohydrates = recipe.NutritionalInfo.Carbohydrates,
					Fat = recipe.NutritionalInfo.Fat,
					Fiber = recipe.NutritionalInfo.Fiber,
					Sugar = recipe.NutritionalInfo.Sugar
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
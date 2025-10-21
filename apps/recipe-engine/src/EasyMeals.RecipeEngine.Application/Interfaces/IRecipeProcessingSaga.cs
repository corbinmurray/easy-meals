namespace EasyMeals.RecipeEngine.Application.Interfaces;

public interface IRecipeProcessingSaga
{
	Task StartProcessingAsync(CancellationToken cancellationToken);
}
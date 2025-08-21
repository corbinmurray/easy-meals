namespace EasyMeals.RecipeEngine.Domain.Interfaces;

public interface IRecipeEngine
{
    /// <summary>
    ///     Runs the recipe engine.
    /// </summary>
    /// <returns></returns>
    Task RunAsync();
}
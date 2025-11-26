namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Recipe;

/// <summary>
///     Represents the meal occasion for a recipe
/// </summary>
public enum MealType
{
    /// <summary>Morning meal</summary>
    Breakfast = 0,

    /// <summary>Midday meal</summary>
    Lunch = 1,

    /// <summary>Evening meal</summary>
    Dinner = 2,

    /// <summary>Light meal between main meals</summary>
    Snack = 3,

    /// <summary>Sweet course typically served after a meal</summary>
    Dessert = 4
}

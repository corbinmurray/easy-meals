namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Recipe;

/// <summary>
///     Represents the course type within a meal
/// </summary>
public enum CourseType
{
    /// <summary>Primary dish of the meal</summary>
    MainCourse = 0,

    /// <summary>Accompaniment to the main course</summary>
    SideDish = 1,

    /// <summary>Small dish served before the main course</summary>
    Appetizer = 2,

    /// <summary>Sweet course served after the main meal</summary>
    Dessert = 3,

    /// <summary>Drink or liquid refreshment</summary>
    Beverage = 4
}

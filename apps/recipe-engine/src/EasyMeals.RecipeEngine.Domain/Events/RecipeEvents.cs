using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Recipe;

namespace EasyMeals.RecipeEngine.Domain.Events;

/// <summary>
///     Event raised when a new recipe is created
/// </summary>
/// <param name="RecipeId">ID of the created recipe</param>
/// <param name="Title">Title of the recipe</param>
/// <param name="SourceProvider">Source provider of the recipe</param>
public sealed record RecipeCreatedEvent(
    Guid RecipeId,
    string Title,
    string SourceProvider) : BaseDomainEvent
{
    public RecipeCreatedEvent(Recipe recipe)
        : this(recipe.Id, recipe.Title, recipe.SourceProvider)
    {
    }
}

/// <summary>
///     Event raised when a recipe is updated
/// </summary>
/// <param name="RecipeId">ID of the updated recipe</param>
public sealed record RecipeUpdatedEvent(Guid RecipeId) : BaseDomainEvent;

/// <summary>
///     Event raised when a recipe title is changed
/// </summary>
/// <param name="RecipeId">ID of the recipe</param>
/// <param name="OldTitle">Previous title</param>
/// <param name="NewTitle">New title</param>
public sealed record RecipeTitleChangedEvent(
    Guid RecipeId,
    string OldTitle,
    string NewTitle) : BaseDomainEvent;

/// <summary>
///     Event raised when an ingredient is added to a recipe
/// </summary>
/// <param name="RecipeId">ID of the recipe</param>
/// <param name="Ingredient">The added ingredient</param>
public sealed record IngredientAddedEvent(
    Guid RecipeId,
    Ingredient Ingredient) : BaseDomainEvent;

/// <summary>
///     Event raised when an ingredient is removed from a recipe
/// </summary>
/// <param name="RecipeId">ID of the recipe</param>
/// <param name="Ingredient">The removed ingredient</param>
public sealed record IngredientRemovedEvent(
    Guid RecipeId,
    Ingredient Ingredient) : BaseDomainEvent;

/// <summary>
///     Event raised when an instruction is added to a recipe
/// </summary>
/// <param name="RecipeId">ID of the recipe</param>
/// <param name="Instruction">The added instruction</param>
public sealed record InstructionAddedEvent(
    Guid RecipeId,
    Instruction Instruction) : BaseDomainEvent;

/// <summary>
///     Event raised when comprehensive nutritional information is added
/// </summary>
/// <param name="RecipeId">ID of the recipe</param>
public sealed record NutritionalInfoCompletedEvent(Guid RecipeId) : BaseDomainEvent;

/// <summary>
///     Event raised when a recipe receives a rating
/// </summary>
/// <param name="RecipeId">ID of the recipe</param>
/// <param name="OldRating">Previous rating (null if first rating)</param>
/// <param name="NewRating">New rating</param>
/// <param name="ReviewCount">Total number of reviews</param>
public sealed record RecipeRatedEvent(
    Guid RecipeId,
    decimal? OldRating,
    decimal NewRating,
    int ReviewCount) : BaseDomainEvent;
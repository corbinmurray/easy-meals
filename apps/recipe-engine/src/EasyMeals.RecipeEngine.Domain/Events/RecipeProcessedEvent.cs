using EasyMeals.RecipeEngine.Domain.Events;

namespace EasyMeals.RecipeEngine.Domain.Events;

/// <summary>
/// Domain event raised when a recipe is successfully processed.
/// </summary>
public record RecipeProcessedEvent(
    Guid RecipeId,
    string Url,
    string ProviderId,
    DateTime ProcessedAt) : IDomainEvent;

using EasyMeals.RecipeEngine.Domain.Events;

namespace EasyMeals.RecipeEngine.Domain.Events;

/// <summary>
/// Domain event raised when a recipe batch is started.
/// </summary>
public record BatchStartedEvent(
    Guid BatchId,
    string ProviderId,
    DateTime StartedAt) : IDomainEvent;

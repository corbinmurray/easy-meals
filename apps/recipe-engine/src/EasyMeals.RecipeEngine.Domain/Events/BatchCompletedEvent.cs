namespace EasyMeals.RecipeEngine.Domain.Events;

/// <summary>
///     Domain event raised when a recipe batch is completed.
/// </summary>
public record BatchCompletedEvent(
	Guid BatchId,
	int ProcessedCount,
	int SkippedCount,
	int FailedCount,
	DateTime CompletedAt) : BaseDomainEvent;
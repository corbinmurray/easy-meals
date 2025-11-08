namespace EasyMeals.RecipeEngine.Domain.Events;

/// <summary>
///     Domain event raised when a processing error occurs.
/// </summary>
public record ProcessingErrorEvent(
	string Url,
	string ProviderId,
	string ErrorMessage,
	DateTime OccurredAt) : BaseDomainEvent;
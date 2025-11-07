namespace EasyMeals.RecipeEngine.Domain.Events;

/// <summary>
/// Domain event raised when an ingredient mapping is missing.
/// </summary>
public record IngredientMappingMissingEvent(
    string ProviderId,
    string ProviderCode,
    string RecipeUrl) : BaseDomainEvent;

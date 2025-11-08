namespace EasyMeals.RecipeEngine.Domain.ValueObjects;

/// <summary>
///     Value object representing an ingredient with provider code and canonical form.
///     Stores both raw provider data and normalized mapping for auditability.
/// </summary>
public class IngredientReference
{
	public string ProviderCode { get; }
	public string? CanonicalForm { get; }
	public string Quantity { get; }
	public int DisplayOrder { get; }

	public IngredientReference(string providerCode, string? canonicalForm, string quantity, int displayOrder)
	{
		if (string.IsNullOrWhiteSpace(providerCode))
			throw new ArgumentException("ProviderCode is required", nameof(providerCode));

		if (string.IsNullOrWhiteSpace(quantity))
			throw new ArgumentException("Quantity is required", nameof(quantity));

		if (displayOrder < 0)
			throw new ArgumentException("DisplayOrder cannot be negative", nameof(displayOrder));

		ProviderCode = providerCode;
		CanonicalForm = canonicalForm;
		Quantity = quantity;
		DisplayOrder = displayOrder;
	}

    /// <summary>
    ///     Indicates whether this ingredient has been normalized (canonical form exists).
    /// </summary>
    public bool IsNormalized => !string.IsNullOrWhiteSpace(CanonicalForm);

	public override bool Equals(object? obj) =>
		obj is IngredientReference other &&
		ProviderCode == other.ProviderCode &&
		CanonicalForm == other.CanonicalForm &&
		Quantity == other.Quantity &&
		DisplayOrder == other.DisplayOrder;

	public override int GetHashCode() => HashCode.Combine(ProviderCode, CanonicalForm, Quantity, DisplayOrder);
}
namespace EasyMeals.RecipeEngine.Domain.Entities;

/// <summary>
///     Aggregate root for mapping provider-specific ingredient codes to canonical forms.
/// </summary>
public class IngredientMapping
{
	public Guid Id { get; private set; }
	public string ProviderId { get; private set; }
	public string ProviderCode { get; private set; }
	public string CanonicalForm { get; private set; }
	public string? Notes { get; private set; }
	public DateTime CreatedAt { get; private set; }
	public DateTime? UpdatedAt { get; private set; }

	private IngredientMapping()
	{
		ProviderId = string.Empty;
		ProviderCode = string.Empty;
		CanonicalForm = string.Empty;
	}

    /// <summary>
    ///     Factory method to create a new ingredient mapping.
    /// </summary>
    public static IngredientMapping Create(string providerId, string providerCode, string canonicalForm, string? notes = null)
	{
		if (string.IsNullOrWhiteSpace(providerId))
			throw new ArgumentException("ProviderId is required", nameof(providerId));

		if (string.IsNullOrWhiteSpace(providerCode))
			throw new ArgumentException("ProviderCode is required", nameof(providerCode));

		if (string.IsNullOrWhiteSpace(canonicalForm))
			throw new ArgumentException("CanonicalForm is required", nameof(canonicalForm));

		return new IngredientMapping
		{
			Id = Guid.NewGuid(),
			ProviderId = providerId,
			ProviderCode = providerCode,
			CanonicalForm = canonicalForm,
			Notes = notes,
			CreatedAt = DateTime.UtcNow
		};
	}

    /// <summary>
    ///     Update the canonical form for this mapping.
    /// </summary>
    public void UpdateCanonicalForm(string newCanonicalForm)
	{
		if (string.IsNullOrWhiteSpace(newCanonicalForm))
			throw new ArgumentException("CanonicalForm is required", nameof(newCanonicalForm));

		CanonicalForm = newCanonicalForm;
		UpdatedAt = DateTime.UtcNow;
	}

    /// <summary>
    ///     Update the notes for this mapping.
    /// </summary>
    public void UpdateNotes(string? notes)
	{
		Notes = notes;
		UpdatedAt = DateTime.UtcNow;
	}
}
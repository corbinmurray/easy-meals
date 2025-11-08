namespace EasyMeals.RecipeEngine.Domain.Entities;

/// <summary>
///     Aggregate root for tracking recipe fingerprints for duplicate detection.
/// </summary>
public class RecipeFingerprint
{
	public Guid Id { get; private set; }
	public string FingerprintHash { get; private set; }
	public string ProviderId { get; private set; }
	public string RecipeUrl { get; private set; }
	public Guid RecipeId { get; private set; }
	public DateTime ProcessedAt { get; private set; }

	private RecipeFingerprint()
	{
		FingerprintHash = string.Empty;
		ProviderId = string.Empty;
		RecipeUrl = string.Empty;
	}

    /// <summary>
    ///     Factory method to create a new recipe fingerprint.
    /// </summary>
    public static RecipeFingerprint Create(string fingerprintHash, string providerId, string recipeUrl, Guid recipeId)
	{
		if (string.IsNullOrWhiteSpace(fingerprintHash))
			throw new ArgumentException("FingerprintHash is required", nameof(fingerprintHash));

		if (fingerprintHash.Length != 64)
			throw new ArgumentException("FingerprintHash must be a 64-character SHA256 hex string", nameof(fingerprintHash));

		if (string.IsNullOrWhiteSpace(providerId))
			throw new ArgumentException("ProviderId is required", nameof(providerId));

		if (string.IsNullOrWhiteSpace(recipeUrl))
			throw new ArgumentException("RecipeUrl is required", nameof(recipeUrl));

		if (!Uri.IsWellFormedUriString(recipeUrl, UriKind.Absolute))
			throw new ArgumentException("RecipeUrl must be a valid absolute URL", nameof(recipeUrl));

		if (recipeId == Guid.Empty)
			throw new ArgumentException("RecipeId cannot be empty", nameof(recipeId));

		return new RecipeFingerprint
		{
			Id = Guid.NewGuid(),
			FingerprintHash = fingerprintHash,
			ProviderId = providerId,
			RecipeUrl = recipeUrl,
			RecipeId = recipeId,
			ProcessedAt = DateTime.UtcNow
		};
	}
}
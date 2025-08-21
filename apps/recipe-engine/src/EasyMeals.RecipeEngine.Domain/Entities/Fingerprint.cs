using EasyMeals.RecipeEngine.Domain.ValueObjects.Fingerprint;

namespace EasyMeals.RecipeEngine.Domain.Entities;

/// <summary>
///     Represents a web scraping fingerprint that tracks URLs and their scraping metadata
///     before content is processed into Recipe entities. This enables change detection,
///     deduplication, and scraping audit trails as outlined in the system architecture.
/// </summary>
public record Fingerprint(
	string Url,
	string ContentHash,
	DateTime ScrapedAt,
	string SourceProvider,
	FingerprintStatus Status,
	ScrapingQuality Quality,
	string? ErrorMessage = null,
	Dictionary<string, object>? Metadata = null)
{
	/// <summary>
	///     Creates a new fingerprint for a successfully scraped URL
	/// </summary>
	public static Fingerprint CreateSuccess(
		string url,
		string contentHash,
		string sourceProvider,
		ScrapingQuality quality = ScrapingQuality.Good,
		Dictionary<string, object>? metadata = null) =>
		new(
			url,
			contentHash,
			DateTime.UtcNow,
			sourceProvider,
			FingerprintStatus.Success,
			quality,
			null,
			metadata);

	/// <summary>
	///     Creates a new fingerprint for a failed scraping attempt
	/// </summary>
	public static Fingerprint CreateFailure(
		string url,
		string sourceProvider,
		string errorMessage,
		Dictionary<string, object>? metadata = null) =>
		new(
			url,
			string.Empty,
			DateTime.UtcNow,
			sourceProvider,
			FingerprintStatus.Failed,
			ScrapingQuality.Poor,
			errorMessage,
			metadata);

	/// <summary>
	///     Determines if this fingerprint represents content that has changed
	///     compared to a previous fingerprint of the same URL
	/// </summary>
	public bool HasContentChanged(Fingerprint? previous) =>
		previous == null || previous.ContentHash != ContentHash;

	/// <summary>
	///     Indicates if this fingerprint represents successfully scraped content
	///     that is ready for recipe processing
	/// </summary>
	public bool IsReadyForProcessing =>
		Status == FingerprintStatus.Success &&
		Quality >= ScrapingQuality.Acceptable &&
		!string.IsNullOrEmpty(ContentHash);
}
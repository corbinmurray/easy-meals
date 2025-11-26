using System.Security.Cryptography;
using System.Text;
using EasyMeals.Platform;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Fingerprint;

namespace EasyMeals.RecipeEngine.Domain.Entities;

/// <summary>
///     Fingerprint aggregate root that tracks web scraping operations and content changes.
///     Responsible for: tracking visited URLs, detecting content changes, and linking to resulting recipes.
/// </summary>
public sealed class Fingerprint : AggregateRoot<Guid>
{
	private readonly Dictionary<string, object> _metadata;

	/// <summary>
	///     Creates a new Fingerprint for successfully scraped content
	/// </summary>
	/// <param name="id">Unique identifier for the fingerprint</param>
	/// <param name="url">URL that was scraped (required)</param>
	/// <param name="rawContent">Raw scraped content (required)</param>
	/// <param name="providerName">Source provider name (required)</param>
	/// <param name="quality">Quality assessment of scraped content</param>
	/// <param name="metadata">Additional metadata about the scraping operation</param>
	public Fingerprint(
		Guid id,
		string url,
		string rawContent,
		string providerName,
		ScrapingQuality quality = ScrapingQuality.Good,
		Dictionary<string, object>? metadata = null)
		: base(id)
	{
		Url = ValidateUrl(url);
		RawContent = ValidateRawContent(rawContent);
		ContentHash = ComputeContentHash(rawContent);
		ProviderName = ValidateProviderName(providerName);
		Quality = quality;
		Status = FingerprintStatus.Success;
		ScrapedAt = DateTime.UtcNow;
		RetryCount = 0;

		_metadata = metadata is not null ? new Dictionary<string, object>(metadata) : [];
	}

	/// <summary>
	///     Creates a new Fingerprint for a failed scraping attempt
	/// </summary>
	/// <param name="id">Unique identifier for the fingerprint</param>
	/// <param name="url">URL that failed to scrape (required)</param>
	/// <param name="providerName">Source provider name (required)</param>
	/// <param name="errorMessage">Error message describing the failure (required)</param>
	/// <param name="metadata">Additional metadata about the scraping operation</param>
	public Fingerprint(
		Guid id,
		string url,
		string providerName,
		string errorMessage,
		Dictionary<string, object>? metadata = null)
		: base(id)
	{
		Url = ValidateUrl(url);
		ProviderName = ValidateProviderName(providerName);
		ErrorMessage = ValidateErrorMessage(errorMessage);
		RawContent = null;
		ContentHash = string.Empty;
		Quality = ScrapingQuality.Poor;
		Status = FingerprintStatus.Failed;
		ScrapedAt = DateTime.UtcNow;
		RetryCount = 0;

		_metadata = metadata is not null ? new Dictionary<string, object>(metadata) : [];
	}

	// Private constructor for reconstitution from persistence
	private Fingerprint() => _metadata = [];

	#region Properties

	/// <summary>URL that was scraped</summary>
	public string Url { get; private set; } = string.Empty;

	/// <summary>Hash of the raw scraped content (for detecting if source page changed)</summary>
	public string ContentHash { get; private set; } = string.Empty;

	/// <summary>Raw scraped content for extraction processing</summary>
	public string? RawContent { get; private set; }

	/// <summary>Timestamp when content was scraped</summary>
	public DateTime ScrapedAt { get; private set; }

	/// <summary>Source provider name</summary>
	public string ProviderName { get; private set; } = string.Empty;

	/// <summary>Current status of the scraping operation</summary>
	public FingerprintStatus Status { get; private set; }

	/// <summary>Quality assessment of the scraped content</summary>
	public ScrapingQuality Quality { get; private set; }

	/// <summary>Error message if scraping failed</summary>
	public string? ErrorMessage { get; private set; }

	/// <summary>Read-only view of metadata</summary>
	public IReadOnlyDictionary<string, object> Metadata => _metadata.AsReadOnly();

	/// <summary>Number of retry attempts</summary>
	public int RetryCount { get; private set; }

	/// <summary>Timestamp when fingerprint was processed into a recipe</summary>
	public DateTime? ProcessedAt { get; private set; }

	/// <summary>ID of the recipe created from this fingerprint (null if not yet processed or failed extraction)</summary>
	public Guid? RecipeId { get; private set; }

	/// <summary>Maximum retry attempts allowed</summary>
	public const int MaxRetryAttempts = 3;

	#endregion

	#region Computed Properties

	/// <summary>
	///     Indicates if this fingerprint has successfully scraped content ready for recipe extraction
	/// </summary>
	public bool IsReadyForProcessing =>
		Status == FingerprintStatus.Success &&
		Quality >= ScrapingQuality.Acceptable &&
		!string.IsNullOrEmpty(ContentHash) &&
		!string.IsNullOrEmpty(RawContent) &&
		ProcessedAt is null;

	/// <summary>
	///     Indicates if this fingerprint represents a successful scraping operation
	/// </summary>
	public bool IsSuccessful => Status == FingerprintStatus.Success && !string.IsNullOrEmpty(ContentHash);

	/// <summary>
	///     Indicates if this fingerprint represents a failed scraping operation
	/// </summary>
	public bool IsFailed => Status == FingerprintStatus.Failed;

	/// <summary>
	///     Indicates if this fingerprint has been processed (attempted recipe extraction)
	/// </summary>
	public bool IsProcessed => ProcessedAt.HasValue;

	/// <summary>
	///     Indicates if this fingerprint resulted in a recipe
	/// </summary>
	public bool HasRecipe => RecipeId.HasValue;

	/// <summary>
	///     Indicates if this fingerprint can be retried
	/// </summary>
	public bool CanRetry =>
		(Status == FingerprintStatus.Failed || Status == FingerprintStatus.Blocked) &&
		RetryCount < MaxRetryAttempts;

	/// <summary>
	///     Indicates if this fingerprint has high quality content
	/// </summary>
	public bool IsHighQuality => Quality >= ScrapingQuality.Good;

	/// <summary>
	///     Gets the age of this fingerprint in hours
	/// </summary>
	public double AgeInHours => (DateTime.UtcNow - ScrapedAt).TotalHours;

	#endregion

	#region Business Methods

	/// <summary>
	///     Determines if the source content has changed compared to a previous fingerprint
	/// </summary>
	public bool HasContentChanged(Fingerprint? previous)
	{
		if (previous is null)
			return true;

		if (!IsSuccessful || !previous.IsSuccessful)
			return false;

		return !string.Equals(ContentHash, previous.ContentHash, StringComparison.Ordinal);
	}

	/// <summary>
	///     Marks this fingerprint as processed, optionally linking to the created recipe
	/// </summary>
	/// <param name="recipeId">ID of the recipe created, or null if extraction failed/was duplicate</param>
	public void MarkAsProcessed(Guid? recipeId = null)
	{
		if (IsProcessed)
			throw new InvalidOperationException("Fingerprint has already been processed");

		if (!IsSuccessful)
			throw new InvalidOperationException("Cannot process a failed fingerprint");

		ProcessedAt = DateTime.UtcNow;
		RecipeId = recipeId;
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Updates the quality assessment of this fingerprint
	/// </summary>
	public void UpdateQuality(ScrapingQuality newQuality)
	{
		if (Quality == newQuality)
			return;

		Quality = newQuality;
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Prepares fingerprint for a retry attempt
	/// </summary>
	public void PrepareForRetry()
	{
		if (!CanRetry)
			throw new InvalidOperationException(
				$"Cannot retry fingerprint. Status: {Status}, RetryCount: {RetryCount}/{MaxRetryAttempts}");

		RetryCount++;
		Status = FingerprintStatus.Processing;
		ErrorMessage = null;
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Updates the fingerprint after a successful retry
	/// </summary>
	public void CompleteRetrySuccess(string rawContent, ScrapingQuality quality)
	{
		RawContent = ValidateRawContent(rawContent);
		ContentHash = ComputeContentHash(rawContent);
		Quality = quality;
		Status = FingerprintStatus.Success;
		ErrorMessage = null;
		ScrapedAt = DateTime.UtcNow;
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Updates the fingerprint after a failed retry
	/// </summary>
	public void CompleteRetryFailure(string errorMessage)
	{
		ErrorMessage = ValidateErrorMessage(errorMessage);
		Status = FingerprintStatus.Failed;
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Marks fingerprint as blocked by anti-bot measures
	/// </summary>
	public void MarkAsBlocked(string reason)
	{
		if (string.IsNullOrWhiteSpace(reason))
			throw new ArgumentException("Block reason is required", nameof(reason));

		Status = FingerprintStatus.Blocked;
		ErrorMessage = $"Blocked: {reason}";
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Clears raw content to free memory after processing
	/// </summary>
	public void ClearRawContent()
	{
		if (!IsProcessed)
			throw new InvalidOperationException("Cannot clear raw content before processing");

		RawContent = null;
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Adds metadata to the fingerprint
	/// </summary>
	public void AddMetadata(string key, object value)
	{
		if (string.IsNullOrWhiteSpace(key))
			throw new ArgumentException("Metadata key cannot be empty", nameof(key));

		ArgumentNullException.ThrowIfNull(value);

		_metadata[key] = value;
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Gets metadata value by key
	/// </summary>
	public T? GetMetadata<T>(string key)
	{
		if (_metadata.TryGetValue(key, out object? value) && value is T typedValue)
			return typedValue;

		return default;
	}

	#endregion

	#region Factory Methods

	/// <summary>
	///     Creates a fingerprint for successfully scraped content
	/// </summary>
	public static Fingerprint CreateSuccess(
		string url,
		string rawContent,
		string providerName,
		ScrapingQuality quality = ScrapingQuality.Good,
		Dictionary<string, object>? metadata = null) =>
		new(Guid.NewGuid(), url, rawContent, providerName, quality, metadata);

	/// <summary>
	///     Creates a fingerprint for a failed scraping attempt
	/// </summary>
	public static Fingerprint CreateFailure(
		string url,
		string providerName,
		string errorMessage,
		Dictionary<string, object>? metadata = null) =>
		new(Guid.NewGuid(), url, providerName, errorMessage, metadata);

	#endregion

	#region Private Methods

	private static string ValidateUrl(string url)
	{
		if (string.IsNullOrWhiteSpace(url))
			throw new ArgumentException("URL cannot be empty", nameof(url));

		if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
			throw new ArgumentException("URL must be a valid absolute URL", nameof(url));

		if (uri.Scheme is not ("http" or "https"))
			throw new ArgumentException("URL must use HTTP or HTTPS protocol", nameof(url));

		return url;
	}

	private static string ValidateProviderName(string providerName)
	{
		if (string.IsNullOrWhiteSpace(providerName))
			throw new ArgumentException("Provider name cannot be empty", nameof(providerName));

		if (providerName.Length > 100)
			throw new ArgumentException("Provider name cannot exceed 100 characters", nameof(providerName));

		return providerName.Trim();
	}

	private static string ValidateErrorMessage(string errorMessage)
	{
		if (string.IsNullOrWhiteSpace(errorMessage))
			throw new ArgumentException("Error message cannot be empty", nameof(errorMessage));

		if (errorMessage.Length > 1000)
			throw new ArgumentException("Error message cannot exceed 1000 characters", nameof(errorMessage));

		return errorMessage.Trim();
	}

	private static string ValidateRawContent(string rawContent)
	{
		if (string.IsNullOrEmpty(rawContent))
			throw new ArgumentException("Raw content cannot be empty", nameof(rawContent));

		if (rawContent.Length > 10_000_000) // 10MB limit
			throw new ArgumentException("Raw content exceeds maximum size limit", nameof(rawContent));

		return rawContent;
	}

	/// <summary>
	///     Computes SHA-256 hash of raw content for change detection
	/// </summary>
	private static string ComputeContentHash(string content)
	{
		if (string.IsNullOrEmpty(content))
			return string.Empty;

		byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
		return Convert.ToHexString(hashBytes).ToLowerInvariant();
	}

	#endregion
}
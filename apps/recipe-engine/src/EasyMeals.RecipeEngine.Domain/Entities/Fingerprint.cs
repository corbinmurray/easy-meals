using System.Security.Cryptography;
using System.Text;
using EasyMeals.RecipeEngine.Domain.Events;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Fingerprint;

namespace EasyMeals.RecipeEngine.Domain.Entities;

/// <summary>
///     Fingerprint aggregate root that tracks web scraping operations and content changes
///     Follows DDD principles with rich domain behavior and proper encapsulation
///     Enables change detection, deduplication, and scraping audit trails
/// </summary>
public sealed class Fingerprint
{
	private readonly List<IDomainEvent> _domainEvents;
	private Dictionary<string, object> _metadata;

	/// <summary>
	///     Creates a new Fingerprint aggregate root from raw content
	///     Automatically computes content and fingerprint hashes from raw content and by normalizing the URL
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
	{
		Id = id;
		Url = ValidateUrl(url);
		ContentHash = ComputeContentHash(rawContent);
		ContentSizeBytes = Encoding.UTF8.GetByteCount(rawContent);
		FingerprintHash = ComputeFingerprintHash(url);
		ProviderName = ValidateProviderName(providerName);
		Quality = quality;
		Status = FingerprintStatus.Success;

		ScrapedAt = DateTime.UtcNow;
		CreatedAt = DateTime.UtcNow;
		UpdatedAt = DateTime.UtcNow;

		_metadata = metadata != null ? new Dictionary<string, object>(metadata) : [];
		_domainEvents = [];

		AddDomainEvent(new FingerprintCreatedEvent(Id, Url, ProviderName, Quality, ContentHash));
	}

	/// <summary>
	///     Computes a lightweight fingerprint hash by normalizing the URL for deduplication
	/// </summary>
	/// <param name="url">URL to normalize and compute the fingerprint hash for</param>
	/// <returns>Normalized fingerprint hash</returns>
	/// <exception cref="NotImplementedException"></exception>
	private static string ComputeFingerprintHash(string url)
	{
		if (string.IsNullOrWhiteSpace(url) ||
		    !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ||
		    !uri.IsWellFormedOriginalString())
			return string.Empty;

		// Align with RecipeFingerprintService.NormalizeUrl:
		// - Lowercase the entire URL (scheme + host + path)
		// - Remove query parameters (everything after '?')
		string normalizedUrl = uri
			.GetComponents(UriComponents.HttpRequestUrl, UriFormat.Unescaped)
			.ToLowerInvariant()
			.Trim();
		int queryIndex = normalizedUrl.IndexOf('?');
		if (queryIndex >= 0)
			normalizedUrl = normalizedUrl[..queryIndex];

		// Compute SHA256 hex string lower-case
		byte[] inputBytes = Encoding.UTF8.GetBytes(normalizedUrl);
		byte[] hashBytes = SHA256.HashData(inputBytes);

		return Convert.ToHexString(hashBytes).ToLowerInvariant();
	}

	/// <summary>
	///     Creates a new Fingerprint aggregate root for failed scraping
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
	{
		Id = id;
		Url = ValidateUrl(url);
		ProviderName = ValidateProviderName(providerName);
		ErrorMessage = ValidateErrorMessage(errorMessage);

		ContentHash = string.Empty;
		Quality = ScrapingQuality.Poor;
		Status = FingerprintStatus.Failed;

		ScrapedAt = DateTime.UtcNow;
		CreatedAt = DateTime.UtcNow;
		UpdatedAt = DateTime.UtcNow;

		_metadata = metadata != null ? new Dictionary<string, object>(metadata) : new Dictionary<string, object>();
		_domainEvents = [];

		AddDomainEvent(new ScrapingFailedEvent(Id, Url, ProviderName, ErrorMessage));
	}

	// Private constructor for reconstitution from persistence
	private Fingerprint()
	{
		_metadata = [];
		_domainEvents = [];
	}

	/// <summary>
	///     Reconstitutes a Fingerprint from persisted data without triggering business rules or events
	/// </summary>
	public static Fingerprint Reconstitute(
		Guid id,
		string url,
		string contentHash,
		string fingerprintHash,
		long? contentByteSize,
		DateTime scrapedAt,
		string providerName,
		FingerprintStatus status,
		ScrapingQuality quality,
		string? errorMessage,
		Dictionary<string, object> metadata,
		DateTime? processedAt,
		Guid? recipeId,
		DateTime createdAt,
		DateTime updatedAt)
	{
		var fingerprint = new Fingerprint
		{
			Id = id,
			Url = url,
			ContentHash = contentHash,
			FingerprintHash = fingerprintHash,
			ContentSizeBytes = contentByteSize,
			ScrapedAt = scrapedAt,
			ProviderName = providerName,
			Status = status,
			Quality = quality,
			ErrorMessage = errorMessage,
			_metadata = metadata ?? [],
			ProcessedAt = processedAt,
			RecipeId = recipeId,
			CreatedAt = createdAt,
			UpdatedAt = updatedAt
		};

		return fingerprint;
	}

	#region Properties

	/// <summary>Unique identifier</summary>
	public Guid Id { get; private set; }

	/// <summary>URL that was scraped</summary>
	public string Url { get; private set; } = string.Empty;

	/// <summary>Hash of the scraped content</summary>
	public string ContentHash { get; private set; } = string.Empty;

	/// <summary>
	///     The number of bytes (size) of the raw content
	/// </summary>
	public long? ContentSizeBytes { get; private set; } = 0;

	/// <summary>
	///     Lightweight, normalized fingerprint hash for deduplication
	/// </summary>
	public string FingerprintHash { get; private set; } = string.Empty;

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

	/// <summary>Timestamp when fingerprint was last processed</summary>
	public DateTime? ProcessedAt { get; private set; }

	/// <summary>ID of the recipe created from this fingerprint</summary>
	public Guid? RecipeId { get; private set; }

	/// <summary>Creation timestamp</summary>
	public DateTime CreatedAt { get; private set; }

	/// <summary>Last update timestamp</summary>
	public DateTime UpdatedAt { get; private set; }

	/// <summary>Read-only view of domain events</summary>
	public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

	#endregion

	#region Computed Properties

	/// <summary>
	///     Indicates if this fingerprint represents successfully scraped content
	///     that is ready for recipe processing
	/// </summary>
	public bool IsReadyForProcessing =>
		Status == FingerprintStatus.Success &&
		Quality >= ScrapingQuality.Acceptable &&
		!string.IsNullOrEmpty(ContentHash) &&
		ProcessedAt == null;

	/// <summary>
	///     Indicates if this fingerprint represents a successful scraping operation
	/// </summary>
	public bool IsSuccessful => Status == FingerprintStatus.Success && !string.IsNullOrEmpty(ContentHash);

	/// <summary>
	///     Indicates if this fingerprint represents a failed scraping operation
	/// </summary>
	public bool IsFailed => Status == FingerprintStatus.Failed;

	/// <summary>
	///     Indicates if this fingerprint has been processed into a recipe
	/// </summary>
	public bool IsProcessed => ProcessedAt.HasValue;

	/// <summary>
	///     Indicates if this fingerprint has high quality content
	/// </summary>
	public bool IsHighQuality => Quality >= ScrapingQuality.Good;

	/// <summary>
	///     Gets the age of this fingerprint in hours
	/// </summary>
	public double AgeInHours => (DateTime.UtcNow - ScrapedAt).TotalHours;

	/// <summary>
	///     Maximum retry attempts allowed
	/// </summary>
	public const int MaxRetryAttempts = 3;

	#endregion

	#region Business Methods

	/// <summary>
	///     Determines if this fingerprint represents content that has changed
	///     compared to a previous fingerprint of the same URL
	/// </summary>
	public bool HasContentChanged(Fingerprint? previous)
	{
		if (previous == null)
			return true;

		if (!IsSuccessful || !previous.IsSuccessful)
			return false;

		bool hasChanged = previous.ContentHash != ContentHash;

		if (hasChanged)
			AddDomainEvent(new ContentChangedEvent(
				Id,
				Url,
				previous.ContentHash,
				ContentHash,
				ProviderName));

		return hasChanged;
	}

	/// <summary>
	///     Updates the quality assessment of this fingerprint
	/// </summary>
	public void UpdateQuality(ScrapingQuality newQuality, string reason)
	{
		if (string.IsNullOrWhiteSpace(reason))
			throw new ArgumentException("Reason for quality update is required", nameof(reason));

		if (Quality == newQuality)
			return; // No change needed

		ScrapingQuality oldQuality = Quality;
		Quality = newQuality;
		UpdatedAt = DateTime.UtcNow;

		AddDomainEvent(new QualityAssessedEvent(Id, Url, oldQuality, newQuality, reason));
	}

	/// <summary>
	///     Marks this fingerprint as processed with optional recipe ID
	/// </summary>
	public void MarkAsProcessed(Guid? recipeId = null)
	{
		if (IsProcessed)
			throw new InvalidOperationException("Fingerprint has already been processed");

		if (!IsReadyForProcessing)
			throw new InvalidOperationException("Fingerprint is not ready for processing");

		ProcessedAt = DateTime.UtcNow;
		RecipeId = recipeId;
		Status = FingerprintStatus.Processing; // Could add a Processed status to enum
		UpdatedAt = DateTime.UtcNow;

		AddDomainEvent(new FingerprintProcessedEvent(Id, Url, recipeId));
	}

	/// <summary>
	///     Updates the fingerprint after a successful retry
	/// </summary>
	public void UpdateAfterSuccessfulRetry(string contentHash, ScrapingQuality quality)
	{
		ContentHash = ValidateContentHash(contentHash);
		Quality = quality;
		Status = FingerprintStatus.Success;
		ErrorMessage = null;
		ScrapedAt = DateTime.UtcNow;
		UpdatedAt = DateTime.UtcNow;

		AddDomainEvent(new FingerprintCreatedEvent(Id, Url, ProviderName, Quality, ContentHash));
	}

	/// <summary>
	///     Updates the fingerprint after a failed retry
	/// </summary>
	public void UpdateAfterFailedRetry(string errorMessage)
	{
		ErrorMessage = ValidateErrorMessage(errorMessage);
		Status = FingerprintStatus.Failed;
		UpdatedAt = DateTime.UtcNow;

		AddDomainEvent(new ScrapingFailedEvent(Id, Url, ProviderName, errorMessage));
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

		AddDomainEvent(new ScrapingFailedEvent(Id, Url, ProviderName, ErrorMessage));
	}

	/// <summary>
	///     Adds metadata to the fingerprint
	/// </summary>
	public void AddMetadata(string key, object value)
	{
		if (string.IsNullOrWhiteSpace(key))
			throw new ArgumentException("Metadata key cannot be empty", nameof(key));

		if (value == null)
			throw new ArgumentNullException(nameof(value));

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

	/// <summary>
	///     Clears all domain events (typically called after persistence)
	/// </summary>
	public void ClearDomainEvents()
	{
		_domainEvents.Clear();
	}

	#endregion

	#region Factory Methods

	/// <summary>
	///     Creates a new fingerprint for a successfully scraped URL
	/// </summary>
	public static Fingerprint CreateSuccess(
		string url,
		string contentHash,
		string providerName,
		ScrapingQuality quality = ScrapingQuality.Good,
		Dictionary<string, object>? metadata = null) =>
		new(Guid.NewGuid(), url, contentHash, providerName, quality, metadata);

	/// <summary>
	///     Creates a new fingerprint for a failed scraping attempt
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

		if (uri.Scheme != "http" && uri.Scheme != "https")
			throw new ArgumentException("URL must use HTTP or HTTPS protocol", nameof(url));

		return url;
	}

	private static string ValidateContentHash(string contentHash)
	{
		if (string.IsNullOrWhiteSpace(contentHash))
			throw new ArgumentException("Content hash cannot be empty", nameof(contentHash));

		if (contentHash.Length < 8)
			throw new ArgumentException("Content hash is too short", nameof(contentHash));

		return contentHash;
	}

	private static string ValidateProviderName(string providerName)
	{
		if (string.IsNullOrWhiteSpace(providerName))
			throw new ArgumentException("Source provider cannot be empty", nameof(providerName));

		if (providerName.Length > 100)
			throw new ArgumentException("Source provider name cannot exceed 100 characters", nameof(providerName));

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

	private void AddDomainEvent(IDomainEvent domainEvent)
	{
		_domainEvents.Add(domainEvent);
	}

	/// <summary>
	///     Computes SHA-256 hash of content for deduplication
	///     Encapsulates content hashing business logic within the aggregate
	/// </summary>
	private static string ComputeContentHash(string content)
	{
		if (string.IsNullOrEmpty(content))
			return string.Empty;

		using var sha256 = SHA256.Create();
		byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
		return Convert.ToHexString(hashBytes).ToLowerInvariant();
	}

	/// <summary>
	///     Validates raw content
	/// </summary>
	private static string ValidateRawContent(string rawContent)
	{
		if (string.IsNullOrEmpty(rawContent))
			throw new ArgumentException("Raw content cannot be empty", nameof(rawContent));

		if (rawContent.Length > 10_000_000) // 10MB limit
			throw new ArgumentException("Raw content exceeds maximum size limit", nameof(rawContent));

		return rawContent;
	}

	#endregion
}
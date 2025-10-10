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
	private readonly Dictionary<string, object> _metadata;

	/// <summary>
	///     Creates a new Fingerprint aggregate root from raw content
	///     Automatically computes content hash from raw content
	/// </summary>
	/// <param name="id">Unique identifier for the fingerprint</param>
	/// <param name="url">URL that was scraped (required)</param>
	/// <param name="rawContent">Raw scraped content (required)</param>
	/// <param name="sourceProvider">Source provider name (required)</param>
	/// <param name="quality">Quality assessment of scraped content</param>
	/// <param name="metadata">Additional metadata about the scraping operation</param>
	public Fingerprint(
		Guid id,
		string url,
		string rawContent,
		string sourceProvider,
		ScrapingQuality quality = ScrapingQuality.Good,
		Dictionary<string, object>? metadata = null)
	{
		Id = id;
		Url = ValidateUrl(url);
		RawContent = ValidateRawContent(rawContent);
		ContentHash = ComputeContentHash(rawContent);
		SourceProvider = ValidateSourceProvider(sourceProvider);
		Quality = quality;
		Status = FingerprintStatus.Success;

		ScrapedAt = DateTime.UtcNow;
		CreatedAt = DateTime.UtcNow;
		UpdatedAt = DateTime.UtcNow;
		RetryCount = 0;

		_metadata = metadata != null ? new Dictionary<string, object>(metadata) : new Dictionary<string, object>();
		_domainEvents = new List<IDomainEvent>();

		AddDomainEvent(new FingerprintCreatedEvent(Id, Url, SourceProvider, Quality, ContentHash));
	}

	/// <summary>
	///     Creates a new Fingerprint aggregate root for failed scraping
	/// </summary>
	/// <param name="id">Unique identifier for the fingerprint</param>
	/// <param name="url">URL that failed to scrape (required)</param>
	/// <param name="sourceProvider">Source provider name (required)</param>
	/// <param name="errorMessage">Error message describing the failure (required)</param>
	/// <param name="metadata">Additional metadata about the scraping operation</param>
	public Fingerprint(
		Guid id,
		string url,
		string sourceProvider,
		string errorMessage,
		Dictionary<string, object>? metadata = null)
	{
		Id = id;
		Url = ValidateUrl(url);
		SourceProvider = ValidateSourceProvider(sourceProvider);
		ErrorMessage = ValidateErrorMessage(errorMessage);

		ContentHash = string.Empty;
		Quality = ScrapingQuality.Poor;
		Status = FingerprintStatus.Failed;

		ScrapedAt = DateTime.UtcNow;
		CreatedAt = DateTime.UtcNow;
		UpdatedAt = DateTime.UtcNow;
		RetryCount = 0;

		_metadata = metadata != null ? new Dictionary<string, object>(metadata) : new Dictionary<string, object>();
		_domainEvents = new List<IDomainEvent>();

		AddDomainEvent(new ScrapingFailedEvent(Id, Url, SourceProvider, ErrorMessage));
	}

	// Private constructor for reconstitution from persistence
	private Fingerprint()
	{
		_metadata = new Dictionary<string, object>();
		_domainEvents = new List<IDomainEvent>();
	}

	#region Properties

	/// <summary>Unique identifier</summary>
	public Guid Id { get; private set; }

	/// <summary>URL that was scraped</summary>
	public string Url { get; private set; } = string.Empty;

	/// <summary>Hash of the scraped content</summary>
	public string ContentHash { get; private set; } = string.Empty;

	/// <summary>Raw scraped content (optional, for debugging and reprocessing)</summary>
	public string? RawContent { get; private set; }

	/// <summary>Timestamp when content was scraped</summary>
	public DateTime ScrapedAt { get; private set; }

	/// <summary>Source provider name</summary>
	public string SourceProvider { get; private set; } = string.Empty;

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
		{
			AddDomainEvent(new ContentChangedEvent(
				Id,
				Url,
				previous.ContentHash,
				ContentHash,
				SourceProvider));
		}

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
	///     Attempts to retry a failed scraping operation
	/// </summary>
	public void Retry(string reason)
	{
		if (!CanRetry)
			throw new InvalidOperationException(
				$"Cannot retry fingerprint. Status: {Status}, RetryCount: {RetryCount}/{MaxRetryAttempts}");

		if (string.IsNullOrWhiteSpace(reason))
			throw new ArgumentException("Retry reason is required", nameof(reason));

		RetryCount++;
		Status = FingerprintStatus.Processing;
		ErrorMessage = null; // Clear previous error
		UpdatedAt = DateTime.UtcNow;

		AddDomainEvent(new FingerprintRetryEvent(Id, Url, RetryCount, reason));
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

		AddDomainEvent(new FingerprintCreatedEvent(Id, Url, SourceProvider, Quality, ContentHash));
	}

	/// <summary>
	///     Updates the fingerprint after a failed retry
	/// </summary>
	public void UpdateAfterFailedRetry(string errorMessage)
	{
		ErrorMessage = ValidateErrorMessage(errorMessage);
		Status = FingerprintStatus.Failed;
		UpdatedAt = DateTime.UtcNow;

		AddDomainEvent(new ScrapingFailedEvent(Id, Url, SourceProvider, errorMessage));
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

		AddDomainEvent(new ScrapingFailedEvent(Id, Url, SourceProvider, ErrorMessage));
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
		string sourceProvider,
		ScrapingQuality quality = ScrapingQuality.Good,
		Dictionary<string, object>? metadata = null) =>
		new(Guid.NewGuid(), url, contentHash, sourceProvider, quality, metadata);

	/// <summary>
	///     Creates a new fingerprint for a failed scraping attempt
	/// </summary>
	public static Fingerprint CreateFailure(
		string url,
		string sourceProvider,
		string errorMessage,
		Dictionary<string, object>? metadata = null) =>
		new(Guid.NewGuid(), url, sourceProvider, errorMessage, metadata);

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

	private static string ValidateSourceProvider(string sourceProvider)
	{
		if (string.IsNullOrWhiteSpace(sourceProvider))
			throw new ArgumentException("Source provider cannot be empty", nameof(sourceProvider));

		if (sourceProvider.Length > 100)
			throw new ArgumentException("Source provider name cannot exceed 100 characters", nameof(sourceProvider));

		return sourceProvider.Trim();
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

		using var sha256 = System.Security.Cryptography.SHA256.Create();
		byte[] hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
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
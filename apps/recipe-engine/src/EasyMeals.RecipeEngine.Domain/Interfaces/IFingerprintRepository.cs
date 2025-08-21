using EasyMeals.RecipeEngine.Domain.Entities;

namespace EasyMeals.RecipeEngine.Domain.Interfaces;

/// <summary>
///     Repository interface for managing Fingerprint entities
///     Supports the pre-processing workflow for scraped content
/// </summary>
public interface IFingerprintRepository
{
	/// <summary>
	///     Finds a fingerprint by URL
	/// </summary>
	Task<Fingerprint?> FindByUrlAsync(string url);

	/// <summary>
	///     Finds a fingerprint by content hash
	/// </summary>
	Task<Fingerprint?> FindByContentHashAsync(string contentHash);

	/// <summary>
	///     Gets all fingerprints for a specific source provider
	/// </summary>
	Task<IEnumerable<Fingerprint>> FindByProviderAsync(
		string provider,
		DateTime? since = null);

	/// <summary>
	///     Gets fingerprints that are ready for processing
	/// </summary>
	Task<IEnumerable<Fingerprint>> FindReadyForProcessingAsync(
		int maxCount = 100);

	/// <summary>
	///     Gets failed fingerprints that can be retried
	/// </summary>
	Task<IEnumerable<Fingerprint>> FindRetryableFingerprintsAsync(
		int maxRetries = 3,
		TimeSpan retryDelay = default);

	/// <summary>
	///     Checks if a URL has been scraped recently
	/// </summary>
	Task<bool> HasBeenScrapedRecentlyAsync(
		string url,
		TimeSpan timeWindow);

	/// <summary>
	///     Gets fingerprints older than the specified age for cleanup
	/// </summary>
	Task<IEnumerable<Fingerprint>> FindStaleAsync(
		TimeSpan maxAge,
		int maxCount = 1000);

	/// <summary>
	///     Adds a new fingerprint
	/// </summary>
	Task<Fingerprint> AddAsync(Fingerprint fingerprint);

	/// <summary>
	///     Updates an existing fingerprint
	/// </summary>
	Task<Fingerprint> UpdateAsync(Fingerprint fingerprint);

	/// <summary>
	///     Deletes fingerprints older than the specified age
	/// </summary>
	Task<int> DeleteStaleAsync(TimeSpan maxAge);

	/// <summary>
	///     Gets statistics about fingerprints for monitoring
	/// </summary>
	Task<FingerprintStatistics> GetStatisticsAsync(
		DateTime? since = null,
		string? provider = null);
}

/// <summary>
///     Statistics about fingerprint data for monitoring and reporting
/// </summary>
public record FingerprintStatistics(
	int TotalCount,
	int SuccessCount,
	int FailedCount,
	int ProcessedCount,
	int ReadyForProcessingCount,
	Dictionary<string, int> StatusCounts,
	Dictionary<string, int> QualityCounts,
	Dictionary<string, int> ProviderCounts,
	DateTime? OldestFingerprint,
	DateTime? NewestFingerprint);
namespace EasyMeals.RecipeEngine.Domain.Entities;

/// <summary>
///     Aggregate root representing a batch of recipes processed within a time window.
///     Enforces batch size and time window invariants.
/// </summary>
public class RecipeBatch
{
	public Guid Id { get; private set; }
	public string ProviderId { get; private set; }
	public int BatchSize { get; private set; }
	public TimeSpan TimeWindow { get; private set; }
	public DateTime StartedAt { get; private set; }
	public DateTime? CompletedAt { get; private set; }
	public int ProcessedCount { get; private set; }
	public int SkippedCount { get; private set; }
	public int FailedCount { get; private set; }
	public BatchStatus Status { get; private set; }

	private readonly List<string> _processedUrls = new();
	public IReadOnlyList<string> ProcessedUrls => _processedUrls.AsReadOnly();

	private readonly List<string> _failedUrls = new();
	public IReadOnlyList<string> FailedUrls => _failedUrls.AsReadOnly();

	private RecipeBatch() => ProviderId = string.Empty;

    /// <summary>
    ///     Factory method to create a new batch.
    /// </summary>
    public static RecipeBatch CreateBatch(string providerId, int batchSize, TimeSpan timeWindow)
	{
		if (string.IsNullOrWhiteSpace(providerId))
			throw new ArgumentException("ProviderId is required", nameof(providerId));

		if (batchSize <= 0)
			throw new ArgumentException("BatchSize must be positive", nameof(batchSize));

		if (timeWindow <= TimeSpan.Zero)
			throw new ArgumentException("TimeWindow must be positive", nameof(timeWindow));

		return new RecipeBatch
		{
			Id = Guid.NewGuid(),
			ProviderId = providerId,
			BatchSize = batchSize,
			TimeWindow = timeWindow,
			StartedAt = DateTime.UtcNow,
			Status = BatchStatus.InProgress,
			ProcessedCount = 0,
			SkippedCount = 0,
			FailedCount = 0
		};
	}

    /// <summary>
    ///     Mark a recipe as successfully processed.
    /// </summary>
    public void MarkRecipeProcessed(string url)
	{
		if (string.IsNullOrWhiteSpace(url))
			throw new ArgumentException("URL is required", nameof(url));

		if (Status != BatchStatus.InProgress)
			throw new InvalidOperationException("Cannot modify batch that is not in progress");

		_processedUrls.Add(url);
		ProcessedCount++;
	}

    /// <summary>
    ///     Mark a recipe as skipped (duplicate or invalid).
    /// </summary>
    public void MarkRecipeSkipped(string url)
	{
		if (string.IsNullOrWhiteSpace(url))
			throw new ArgumentException("URL is required", nameof(url));

		if (Status != BatchStatus.InProgress)
			throw new InvalidOperationException("Cannot modify batch that is not in progress");

		SkippedCount++;
	}

    /// <summary>
    ///     Mark a recipe as failed (permanent error).
    /// </summary>
    public void MarkRecipeFailed(string url)
	{
		if (string.IsNullOrWhiteSpace(url))
			throw new ArgumentException("URL is required", nameof(url));

		if (Status != BatchStatus.InProgress)
			throw new InvalidOperationException("Cannot modify batch that is not in progress");

		_failedUrls.Add(url);
		FailedCount++;
	}

    /// <summary>
    ///     Complete the batch processing.
    /// </summary>
    public void CompleteBatch()
	{
		if (Status != BatchStatus.InProgress)
			throw new InvalidOperationException("Cannot complete batch that is not in progress");

		Status = BatchStatus.Completed;
		CompletedAt = DateTime.UtcNow;
	}

    /// <summary>
    ///     Determine if batch should stop processing based on size and time window.
    /// </summary>
    public bool ShouldStopProcessing(DateTime currentTime)
	{
		if (ProcessedCount >= BatchSize)
			return true;

		TimeSpan elapsed = currentTime - StartedAt;
		if (elapsed >= TimeWindow)
			return true;

		return false;
	}

    /// <summary>
    ///     Get the elapsed processing time.
    /// </summary>
    public TimeSpan GetElapsedTime()
	{
		DateTime endTime = CompletedAt ?? DateTime.UtcNow;
		return endTime - StartedAt;
	}

    /// <summary>
    ///     Get the total number of recipes processed (including skipped and failed).
    /// </summary>
    public int GetTotalProcessed() => ProcessedCount + SkippedCount + FailedCount;
}

public enum BatchStatus
{
	Pending,
	InProgress,
	Completed,
	Failed
}
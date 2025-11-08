using EasyMeals.RecipeEngine.Domain.Entities;

namespace EasyMeals.RecipeEngine.Domain.Interfaces;

/// <summary>
///     Repository interface for managing SagaState aggregates
///     Provides persistence operations for workflow state management and resumability
///     Follows DDD repository pattern with domain-specific query methods
/// </summary>
public interface ISagaStateRepository
{
    /// <summary>
    ///     Gets a saga state by its unique identifier
    /// </summary>
    /// <param name="id">The saga state identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The saga state if found, null otherwise</returns>
    Task<SagaState?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a saga state by correlation ID
    /// </summary>
    /// <param name="correlationId">The correlation identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The saga state if found, null otherwise</returns>
    Task<SagaState?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all saga states of a specific type
    /// </summary>
    /// <param name="sagaType">The type of saga to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of saga states matching the type</returns>
    Task<IEnumerable<SagaState>> GetBySagaTypeAsync(string sagaType, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets saga states by their current status
    /// </summary>
    /// <param name="status">The status to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of saga states with the specified status</returns>
    Task<IEnumerable<SagaState>> GetByStatusAsync(SagaStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets saga states that can be resumed (running or paused)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of resumable saga states</returns>
    Task<IEnumerable<SagaState>> GetResumableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets saga states that have failed and may need retry
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts allowed</param>
    /// <param name="retryDelay">Minimum time since failure before allowing retry</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of failed saga states eligible for retry</returns>
    Task<IEnumerable<SagaState>> GetFailedForRetryAsync(
		int maxRetries = 3,
		TimeSpan retryDelay = default,
		CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets saga states that have exceeded the timeout period
    /// </summary>
    /// <param name="timeout">The timeout duration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of timed-out saga states</returns>
    Task<IEnumerable<SagaState>> GetTimedOutAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets saga states older than the specified age for cleanup
    /// </summary>
    /// <param name="maxAge">Maximum age of saga states to keep</param>
    /// <param name="statusFilter">Optional status filter for cleanup</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of stale saga states for cleanup</returns>
    Task<IEnumerable<SagaState>> GetStaleAsync(
		TimeSpan maxAge,
		SagaStatus? statusFilter = null,
		CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds a new saga state to the repository
    /// </summary>
    /// <param name="sagaState">The saga state to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The added saga state with any generated identifiers</returns>
    Task<SagaState> AddAsync(SagaState sagaState, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates an existing saga state
    /// </summary>
    /// <param name="sagaState">The saga state to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated saga state</returns>
    Task<SagaState> UpdateAsync(SagaState sagaState, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a saga state by its identifier
    /// </summary>
    /// <param name="id">The saga state identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the saga state was deleted, false if not found</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes saga states older than the specified age
    /// </summary>
    /// <param name="maxAge">Maximum age of saga states to keep</param>
    /// <param name="statusFilter">Optional status filter for deletion</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of saga states deleted</returns>
    Task<int> DeleteStaleAsync(
		TimeSpan maxAge,
		SagaStatus? statusFilter = null,
		CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a saga state exists with the specified correlation ID
    /// </summary>
    /// <param name="correlationId">The correlation identifier to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if a saga state exists with the correlation ID</returns>
    Task<bool> ExistsByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets statistics about saga states for monitoring and reporting
    /// </summary>
    /// <param name="since">Optional date filter for statistics</param>
    /// <param name="sagaType">Optional saga type filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Statistics about saga states</returns>
    Task<SagaStateStatistics> GetStatisticsAsync(
		DateTime? since = null,
		string? sagaType = null,
		CancellationToken cancellationToken = default);
}

/// <summary>
///     Statistics about saga state data for monitoring and reporting
/// </summary>
public record SagaStateStatistics(
	int TotalCount,
	int RunningCount,
	int CompletedCount,
	int FailedCount,
	int PausedCount,
	Dictionary<string, int> StatusCounts,
	Dictionary<string, int> SagaTypeCounts,
	DateTime? OldestSagaState,
	DateTime? NewestSagaState,
	TimeSpan? AverageExecutionTime,
	double SuccessRate);
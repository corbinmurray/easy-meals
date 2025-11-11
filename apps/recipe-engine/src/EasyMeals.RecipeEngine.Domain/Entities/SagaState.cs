using EasyMeals.RecipeEngine.Domain.Events;

namespace EasyMeals.RecipeEngine.Domain.Entities;

/// <summary>
///     SagaState aggregate root that persists workflow state for resumability
///     Enables checkpoint-based recovery and replay capabilities across application runs
///     Follows DDD principles with rich domain behavior and proper encapsulation
/// </summary>
public sealed class SagaState
{
    private readonly List<IDomainEvent> _domainEvents;

    /// <summary>
    ///     Creates a new SagaState aggregate root for a recipe processing saga
    /// </summary>
    /// <param name="id">Unique identifier for the saga state</param>
    /// <param name="sagaType">Type of saga (e.g., "RecipeProcessing")</param>
    /// <param name="correlationId">Correlation ID to link related operations</param>
    public SagaState(Guid id, string sagaType, Guid correlationId)
    {
        Id = id;
        SagaType = ValidateSagaType(sagaType);
        CorrelationId = correlationId;

        Status = SagaStatus.Created;
        CurrentPhase = "NotStarted";
        PhaseProgress = 0;

        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        StartedAt = null;
        CompletedAt = null;

        _domainEvents = [];
        StateData = new Dictionary<string, object>();
        CheckpointData = new Dictionary<string, object>();
        Metrics = new SagaMetrics();

        AddDomainEvent(new SagaStateCreatedEvent(Id, SagaType, CorrelationId));
    }

    // Private constructor for reconstitution from persistence
    private SagaState()
    {
        _domainEvents = [];
        StateData = new Dictionary<string, object>();
        CheckpointData = new Dictionary<string, object>();
        Metrics = new SagaMetrics();
    }

    #region Properties

    /// <summary>Unique identifier</summary>
    public Guid Id { get; private set; }

    /// <summary>Type of saga (e.g., "RecipeProcessing")</summary>
    public string SagaType { get; private set; } = string.Empty;

    /// <summary>Correlation ID for linking related operations</summary>
    public Guid CorrelationId { get; private set; }

    /// <summary>Current status of the saga</summary>
    public SagaStatus Status { get; private set; }

    /// <summary>Current phase of execution</summary>
    public string CurrentPhase { get; private set; } = string.Empty;

    /// <summary>Progress within current phase (0-100)</summary>
    public int PhaseProgress { get; private set; }

    /// <summary>Serialized state data for the saga</summary>
    public Dictionary<string, object> StateData { get; private set; }

    /// <summary>Checkpoint data for resumability</summary>
    public Dictionary<string, object> CheckpointData { get; private set; }

    /// <summary>Performance and progress metrics</summary>
    public SagaMetrics Metrics { get; private set; }

    /// <summary>Error message if saga failed</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Stack trace if saga failed</summary>
    public string? ErrorStackTrace { get; private set; }

    /// <summary>Timestamp when saga was created</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Timestamp when saga execution started</summary>
    public DateTime? StartedAt { get; private set; }

    /// <summary>Timestamp when saga was last updated</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Timestamp when saga completed (success or failure)</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>Read-only view of domain events</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    #endregion

    #region Computed Properties

    /// <summary>Whether the saga is currently running</summary>
    public bool IsRunning => Status == SagaStatus.Running;

    /// <summary>Whether the saga has completed successfully</summary>
    public bool IsCompleted => Status == SagaStatus.Completed;

    /// <summary>Whether the saga has failed</summary>
    public bool IsFailed => Status == SagaStatus.Failed;

    /// <summary>Whether the saga can be resumed</summary>
    public bool CanResume => Status == SagaStatus.Running || Status == SagaStatus.Paused;

    /// <summary>Total execution time if completed</summary>
    public TimeSpan? TotalExecutionTime =>
        CompletedAt.HasValue && StartedAt.HasValue
            ? CompletedAt.Value - StartedAt.Value
            : null;

    /// <summary>Whether the saga has checkpoint data for resumability</summary>
    public bool HasCheckpoint => CheckpointData.Any();

    #endregion

    #region Business Methods

    /// <summary>
    ///     Starts the saga execution
    /// </summary>
    public void Start()
    {
        if (Status != SagaStatus.Created && Status != SagaStatus.Paused)
            throw new InvalidOperationException($"Cannot start saga in status {Status}");

        Status = SagaStatus.Running;
        StartedAt ??= DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new SagaStateStartedEvent(Id, CorrelationId));
    }

    /// <summary>
    ///     Updates the current phase and progress
    /// </summary>
    public void UpdateProgress(string phase, int progress, Dictionary<string, object>? stateData = null)
    {
        if (progress < 0 || progress > 100)
            throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be between 0 and 100");

        CurrentPhase = ValidatePhase(phase);
        PhaseProgress = progress;
        UpdatedAt = DateTime.UtcNow;

        if (stateData != null)
            foreach (KeyValuePair<string, object> kvp in stateData)
            {
                StateData[kvp.Key] = kvp.Value;
            }

        Metrics.LastProgressUpdate = DateTime.UtcNow;
        Metrics.TotalUpdates++;

        AddDomainEvent(new SagaProgressUpdatedEvent(Id, CorrelationId, phase, progress));
    }

    /// <summary>
    ///     Creates a checkpoint for resumability
    /// </summary>
    public void CreateCheckpoint(string checkpointName, Dictionary<string, object> checkpointData)
    {
        if (string.IsNullOrWhiteSpace(checkpointName))
            throw new ArgumentException("Checkpoint name cannot be empty", nameof(checkpointName));

        CheckpointData[checkpointName] = checkpointData;
        CheckpointData["LastCheckpointTime"] = DateTime.UtcNow;
        CheckpointData["LastCheckpointPhase"] = CurrentPhase;
        CheckpointData["LastCheckpointProgress"] = PhaseProgress;

        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new SagaCheckpointCreatedEvent(Id, CorrelationId, checkpointName));
    }

    /// <summary>
    ///     Pauses the saga execution
    /// </summary>
    public void Pause()
    {
        if (Status != SagaStatus.Running)
            throw new InvalidOperationException($"Cannot pause saga in status {Status}");

        Status = SagaStatus.Paused;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new SagaStatePausedEvent(Id, CorrelationId));
    }

    /// <summary>
    ///     Resumes the saga execution
    /// </summary>
    public void Resume()
    {
        if (Status != SagaStatus.Paused)
            throw new InvalidOperationException($"Cannot resume saga in status {Status}");

        Status = SagaStatus.Running;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new SagaStateResumedEvent(Id, CorrelationId));
    }

    /// <summary>
    ///     Marks the saga as completed successfully
    /// </summary>
    public void Complete(Dictionary<string, object>? finalMetrics = null)
    {
        if (Status == SagaStatus.Completed)
            return; // Already completed

        Status = SagaStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        if (finalMetrics != null)
            foreach (KeyValuePair<string, object> kvp in finalMetrics)
            {
                Metrics.AdditionalMetrics[kvp.Key] = kvp.Value;
            }

        AddDomainEvent(new SagaStateCompletedEvent(Id, CorrelationId, TotalExecutionTime));
    }

    /// <summary>
    ///     Marks the saga as failed
    /// </summary>
    public void Fail(string errorMessage, string? stackTrace = null)
    {
        if (Status == SagaStatus.Failed)
            return; // Already failed

        Status = SagaStatus.Failed;
        ErrorMessage = ValidateErrorMessage(errorMessage);
        ErrorStackTrace = stackTrace;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        Metrics.FailureCount++;

        AddDomainEvent(new SagaStateFailedEvent(Id, CorrelationId, errorMessage));
    }

    /// <summary>
    ///     Updates performance metrics
    /// </summary>
    public void UpdateMetrics(int itemsProcessed = 0, int itemsFailed = 0, TimeSpan? phaseDuration = null)
    {
        Metrics.ItemsProcessed += itemsProcessed;
        Metrics.ItemsFailed += itemsFailed;

        if (phaseDuration.HasValue) Metrics.TotalProcessingTime += phaseDuration.Value;

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    ///     Gets checkpoint data for resumability
    /// </summary>
    public Dictionary<string, object>? GetCheckpoint(string checkpointName)
    {
        if (CheckpointData.TryGetValue(checkpointName, out object? data) && data is Dictionary<string, object> checkpointData) return checkpointData;

        return null;
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
    ///     Creates a new saga state for recipe processing
    /// </summary>
    public static SagaState CreateForRecipeProcessing(Guid correlationId, string sagaType = "RecipeProcessingSaga") =>
        new(Guid.NewGuid(), sagaType, correlationId);

    /// <summary>
    ///     Reconstitutes a saga state from persisted data
    /// </summary>
    public static SagaState Reconstitute(
        Guid id,
        string sagaType,
        Guid correlationId,
        SagaStatus status,
        string currentPhase,
        int phaseProgress,
        Dictionary<string, object> stateData,
        Dictionary<string, object> checkpointData,
        SagaMetrics metrics,
        string? errorMessage,
        string? errorStackTrace,
        DateTime createdAt,
        DateTime? startedAt,
        DateTime updatedAt,
        DateTime? completedAt)
    {
        var sagaState = new SagaState
        {
            Id = id,
            SagaType = sagaType,
            CorrelationId = correlationId,
            Status = status,
            CurrentPhase = currentPhase,
            PhaseProgress = phaseProgress,
            StateData = stateData ?? new Dictionary<string, object>(),
            CheckpointData = checkpointData ?? new Dictionary<string, object>(),
            Metrics = metrics ?? new SagaMetrics(),
            ErrorMessage = errorMessage,
            ErrorStackTrace = errorStackTrace,
            CreatedAt = createdAt,
            StartedAt = startedAt,
            UpdatedAt = updatedAt,
            CompletedAt = completedAt
        };

        return sagaState;
    }

    #endregion

    #region Private Methods

    private static string ValidateSagaType(string sagaType)
    {
        if (string.IsNullOrWhiteSpace(sagaType))
            throw new ArgumentException("Saga type cannot be empty", nameof(sagaType));

        if (sagaType.Length > 100)
            throw new ArgumentException("Saga type cannot exceed 100 characters", nameof(sagaType));

        return sagaType.Trim();
    }

    private static string ValidatePhase(string phase)
    {
        if (string.IsNullOrWhiteSpace(phase))
            throw new ArgumentException("Phase cannot be empty", nameof(phase));

        if (phase.Length > 100)
            throw new ArgumentException("Phase cannot exceed 100 characters", nameof(phase));

        return phase.Trim();
    }

    private static string ValidateErrorMessage(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be empty", nameof(errorMessage));

        if (errorMessage.Length > 2000)
            throw new ArgumentException("Error message cannot exceed 2000 characters", nameof(errorMessage));

        return errorMessage.Trim();
    }

    private void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    #endregion
}

/// <summary>
///     Status enumeration for saga state
/// </summary>
public enum SagaStatus
{
    None,
    Created,
    Running,
    Paused,
    Completed,
    Failed
}

/// <summary>
///     Metrics for saga performance tracking
/// </summary>
public class SagaMetrics
{
    public int ItemsProcessed { get; set; }
    public int ItemsFailed { get; set; }
    public int TotalUpdates { get; set; }
    public int FailureCount { get; set; }
    public TimeSpan TotalProcessingTime { get; set; }
    public DateTime? LastProgressUpdate { get; set; }
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
}
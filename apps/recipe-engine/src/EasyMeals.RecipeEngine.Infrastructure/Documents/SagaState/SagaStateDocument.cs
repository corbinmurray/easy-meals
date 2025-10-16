using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.Shared.Data.Attributes;
using EasyMeals.Shared.Data.Documents;
using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.RecipeEngine.Infrastructure.Documents.SagaState;

/// <summary>
///     MongoDB document for Saga state persistence.
///     Mirrors domain entity structure while optimizing for NoSQL storage.
///     Infrastructure layer can reference domain types (SagaStatus) following DDD dependency direction.
/// </summary>
[BsonCollection("saga_states")]
public class SagaStateDocument : BaseSoftDeletableDocument
{
	/// <summary>Type of saga (e.g., "RecipeProcessing")</summary>
	[BsonElement("sagaType")]
	[BsonRequired]
	public string SagaType { get; set; } = string.Empty;

	/// <summary>Correlation ID for linking related operations</summary>
	[BsonElement("correlationId")]
	[BsonRequired]
	public Guid CorrelationId { get; set; }

	/// <summary>Current status of the saga</summary>
	[BsonElement("status")]
	[BsonRequired]
	public SagaStatus Status { get; set; }

	/// <summary>Current phase of execution</summary>
	[BsonElement("currentPhase")]
	[BsonRequired]
	public string CurrentPhase { get; set; } = string.Empty;

	/// <summary>Progress within current phase (0-100)</summary>
	[BsonElement("phaseProgress")]
	[BsonRequired]
	public int PhaseProgress { get; set; }

	/// <summary>Serialized state data for the saga</summary>
	[BsonElement("stateData")]
	public Dictionary<string, object> StateData { get; set; } = new();

	/// <summary>Checkpoint data for resumability</summary>
	[BsonElement("checkpointData")]
	public Dictionary<string, object> CheckpointData { get; set; } = new();

	/// <summary>Items processed during saga execution</summary>
	[BsonElement("itemsProcessed")]
	public int ItemsProcessed { get; set; }

	/// <summary>Items that failed processing</summary>
	[BsonElement("itemsFailed")]
	public int ItemsFailed { get; set; }

	/// <summary>Total number of progress updates</summary>
	[BsonElement("totalUpdates")]
	public int TotalUpdates { get; set; }

	/// <summary>Number of times the saga has failed</summary>
	[BsonElement("failureCount")]
	public int FailureCount { get; set; }

	/// <summary>Total processing time as ticks</summary>
	[BsonElement("totalProcessingTimeTicks")]
	public long TotalProcessingTimeTicks { get; set; }

	/// <summary>Timestamp of last progress update</summary>
	[BsonElement("lastProgressUpdate")]
	[BsonIgnoreIfNull]
	public DateTime? LastProgressUpdate { get; set; }

	/// <summary>Additional metrics as key-value pairs</summary>
	[BsonElement("additionalMetrics")]
	public Dictionary<string, object> AdditionalMetrics { get; set; } = new();

	/// <summary>Error message if saga failed</summary>
	[BsonElement("errorMessage")]
	[BsonIgnoreIfNull]
	public string? ErrorMessage { get; set; }

	/// <summary>Stack trace if saga failed</summary>
	[BsonElement("errorStackTrace")]
	[BsonIgnoreIfNull]
	public string? ErrorStackTrace { get; set; }

	/// <summary>Timestamp when saga execution started</summary>
	[BsonElement("startedAt")]
	[BsonIgnoreIfNull]
	public DateTime? StartedAt { get; set; }

	/// <summary>Timestamp when saga completed (success or failure)</summary>
	[BsonElement("completedAt")]
	[BsonIgnoreIfNull]
	public DateTime? CompletedAt { get; set; }

	/// <summary>
	///     Converts domain entity to document for persistence
	/// </summary>
	public static SagaStateDocument FromDomain(Domain.Entities.SagaState sagaState)
	{
		return new SagaStateDocument
		{
			Id = sagaState.Id.ToString(),
			SagaType = sagaState.SagaType,
			CorrelationId = sagaState.CorrelationId,
			Status = sagaState.Status,
			CurrentPhase = sagaState.CurrentPhase,
			PhaseProgress = sagaState.PhaseProgress,
			StateData = sagaState.StateData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
			CheckpointData = sagaState.CheckpointData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
			ItemsProcessed = sagaState.Metrics.ItemsProcessed,
			ItemsFailed = sagaState.Metrics.ItemsFailed,
			TotalUpdates = sagaState.Metrics.TotalUpdates,
			FailureCount = sagaState.Metrics.FailureCount,
			TotalProcessingTimeTicks = sagaState.Metrics.TotalProcessingTime.Ticks,
			LastProgressUpdate = sagaState.Metrics.LastProgressUpdate,
			AdditionalMetrics = sagaState.Metrics.AdditionalMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
			ErrorMessage = sagaState.ErrorMessage,
			ErrorStackTrace = sagaState.ErrorStackTrace,
			CreatedAt = sagaState.CreatedAt,
			StartedAt = sagaState.StartedAt,
			UpdatedAt = sagaState.UpdatedAt,
			CompletedAt = sagaState.CompletedAt
		};
	}

	/// <summary>
	///     Converts document to domain entity for business logic
	/// </summary>
	public Domain.Entities.SagaState ToDomain()
	{
		var metrics = new SagaMetrics
		{
			ItemsProcessed = ItemsProcessed,
			ItemsFailed = ItemsFailed,
			TotalUpdates = TotalUpdates,
			FailureCount = FailureCount,
			TotalProcessingTime = TimeSpan.FromTicks(TotalProcessingTimeTicks),
			LastProgressUpdate = LastProgressUpdate,
			AdditionalMetrics = AdditionalMetrics?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>()
		};

		return Domain.Entities.SagaState.Reconstitute(
			Guid.Parse(Id),
			SagaType,
			CorrelationId,
			Status,
			CurrentPhase,
			PhaseProgress,
			StateData ?? new Dictionary<string, object>(),
			CheckpointData ?? new Dictionary<string, object>(),
			metrics,
			ErrorMessage,
			ErrorStackTrace,
			CreatedAt,
			StartedAt,
			UpdatedAt,
			CompletedAt);
	}
}
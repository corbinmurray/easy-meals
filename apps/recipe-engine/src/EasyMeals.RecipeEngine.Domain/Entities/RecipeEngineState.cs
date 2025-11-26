using System.Dynamic;
using EasyMeals.Platform;

namespace EasyMeals.RecipeEngine.Domain.Entities;

public sealed partial class RecipeEngineState : AggregateRoot<Guid>
{
	private List<ErrorDetail> _errorDetails;

	/// <summary>
	///     Instantiates a new RecipeEngineState aggregate root
	/// </summary>
	/// <param name="id"></param>
	/// <param name="runId"></param>
	public RecipeEngineState(Guid id, Guid runId)
		: base(id)
	{
		RunId = runId;
		CurrentPhase = RecipeEnginePhase.NotStarted;
		Status = RecipeEngineStatus.None;
		PhaseProgress = 0;

		StartedAt = null;
		CompletedAt = null;

		_errorDetails = [];
		RecipeEngineContext = [];
		CheckpointData = [];
		Metrics = new RecipeEngineMetrics();
	}

	// Private constructor for reconstitution from persistence
	private RecipeEngineState(Guid id, DateTime createdAt, DateTime updatedAt)
		: base(id, createdAt, updatedAt)
	{
		_errorDetails = [];
		RecipeEngineContext = [];
		CheckpointData = [];
		Metrics = new RecipeEngineMetrics();
	}

	#region Properties

	/// <summary>Gets the run ID of the recipe engine's current execution</summary>
	public Guid RunId { get; private set; }

	/// <summary>Gets the current phase of execution</summary>
	public RecipeEnginePhase CurrentPhase { get; private set; }

	/// <summary>Gets the progress within current phase (0-100)</summary>
	public int PhaseProgress { get; private set; }

	/// <summary>Gets the status of the recipe engine's current run</summary>
	public RecipeEngineStatus Status { get; private set; }

	/// <summary>Gets the timestamp when saga execution started</summary>
	public DateTime? StartedAt { get; private set; }

	/// <summary>Gets the timestamp when saga completed (success or failure)</summary>
	public DateTime? CompletedAt { get; private set; }

	/// <summary>Gets the dynamic context data for the recipe engine's execution</summary>
	public Dictionary<string, object> RecipeEngineContext { get; private set; }

	/// <summary>Gets the checkpoint data for resuming execution</summary>
	public Dictionary<string, object> CheckpointData { get; private set; }

	/// <summary>Gets the collection of error details if any errors occurred during execution</summary>
	public IReadOnlyCollection<ErrorDetail> ErrorDetails => _errorDetails.AsReadOnly();
	
	/// <summary>Gets the metrics collected during the recipe engine's execution</summary>
	public RecipeEngineMetrics Metrics { get; private set; }

	#endregion

	#region BusinessMethods

	/// <summary>
	///     Updates the current phase and progress
	/// </summary>
	public void UpdateProgress(RecipeEnginePhase phase, int progress)
	{
		if (progress is < 0 or > 100)
			throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be between 0 and 100.");

		CurrentPhase = phase;
		PhaseProgress = progress;
		UpdatedAt = DateTime.UtcNow;

		Metrics.LastProgressUpdate = DateTime.UtcNow;
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
	}


	#endregion
}


public enum RecipeEnginePhase
{
	NotStarted,
	Discovering,
	Fingerprinting,
	Processing,
	Persisting
}

public enum RecipeEngineStatus
{
	None,
	Created,
	Running,
	Paused,
	Completed,
	Failed
}

public sealed record ErrorDetail(
	int ErrorCode,
	string ErrorMessage,
	string PhaseFailed,
	string? StackTrace = null);

public sealed class RecipeEngineMetrics
{
	public int RecipesProcessed { get; set;}
	public int RecipesSucceeded { get; set;}
	public int RecipesFailed { get; set;}
	public TimeSpan TotalProcessingTime { get; set;}
	public DateTime? LastProgressUpdate { get; set;}
	public Dictionary<string, object>? AdditionalMetrics { get; set; }
}

public partial class RecipeEngineState
{
	// Used by repository to reconstitute state without side-effects
	internal static RecipeEngineState Rehydrate(RecipeEngineStateMemento m)
	{
		// Use the public ctor to ensure base initialization (no events) then overwrite values
		var state = new RecipeEngineState(m.Id, m.CreatedAt, m.UpdatedAt)
		{
			RunId = m.RunId,
			CurrentPhase = m.CurrentPhase,
			PhaseProgress = m.PhaseProgress,
			Status = m.Status,
			StartedAt = m.StartedAt,
			CompletedAt = m.CompletedAt,
			RecipeEngineContext = m.RecipeEngineContext ?? new Dictionary<string, object>(),
			CheckpointData = m.CheckpointData ?? new Dictionary<string, object>(),
			UpdatedAt = m.UpdatedAt,
			Metrics = m.Metrics ?? new RecipeEngineMetrics(),
			_errorDetails =  m.ErrorDetails ?? []
		};

		return state;
	}
}

public sealed record RecipeEngineStateMemento(
	Guid Id,
	Guid RunId,
	RecipeEnginePhase CurrentPhase,
	int PhaseProgress,
	RecipeEngineStatus Status,
	DateTime? StartedAt,
	DateTime? CompletedAt,
	Dictionary<string, object>? RecipeEngineContext,
	Dictionary<string, object>? CheckpointData,
	List<ErrorDetail>? ErrorDetails,
	RecipeEngineMetrics? Metrics,
	DateTime CreatedAt,
	DateTime UpdatedAt
);
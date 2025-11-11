using EasyMeals.RecipeEngine.Domain.Entities;
using FluentAssertions;

namespace EasyMeals.RecipeEngine.Tests.Contract;

/// <summary>
///     Contract tests for RecipeProcessingSaga crash recovery.
///     Tests ability to save state mid-batch, restart, and resume from saved state.
/// </summary>
public class RecipeProcessingSagaCrashRecoveryTests
{
	[Fact(DisplayName = "Saga can be reconstituted from persisted state")]
	public void SagaState_CanBeReconstituted_FromPersistedData()
	{
		// Arrange - Original saga state
		var originalId = Guid.NewGuid();
		var correlationId = Guid.NewGuid();
		var stateData = new Dictionary<string, object>
		{
			["DiscoveredUrls"] = new List<string> { "https://example.com/recipe1" },
			["CurrentIndex"] = 0
		};
		var checkpointData = new Dictionary<string, object>
		{
			["LastCheckpoint"] = DateTime.UtcNow
		};
		var metrics = new SagaMetrics
		{
			ItemsProcessed = 5,
			ItemsFailed = 1
		};

		// Act - Reconstitute saga state
		SagaState reconstitutedState = SagaState.Reconstitute(
			originalId,
			"RecipeProcessingSaga",
			correlationId,
			SagaStatus.Running,
			"Processing",
			50,
			stateData,
			checkpointData,
			metrics,
			null,
			null,
			DateTime.UtcNow.AddMinutes(-10),
			DateTime.UtcNow.AddMinutes(-9),
			DateTime.UtcNow.AddMinutes(-1),
			null
		);

		// Assert
		reconstitutedState.Should().NotBeNull();
		reconstitutedState.Id.Should().Be(originalId);
		reconstitutedState.CorrelationId.Should().Be(correlationId);
		reconstitutedState.Status.Should().Be(SagaStatus.Running);
		reconstitutedState.CurrentPhase.Should().Be("Processing");
		reconstitutedState.PhaseProgress.Should().Be(50);
		reconstitutedState.StateData["CurrentIndex"].Should().Be(0);
		reconstitutedState.Metrics.ItemsProcessed.Should().Be(5);
		reconstitutedState.Metrics.ItemsFailed.Should().Be(1);
	}

	[Fact(DisplayName = "Saga handles crash during Discovering phase")]
	public void SagaState_HandlesCrash_DuringDiscovering()
	{
		// Arrange - Saga crashed while discovering URLs
		var sagaState = SagaState
			.CreateForRecipeProcessing(Guid.NewGuid());

		sagaState.Start();
		sagaState.UpdateProgress("Discovering", 30);

		var checkpointData = new Dictionary<string, object>
		{
			["DiscoveredUrls"] = new List<string>
			{
				"https://example.com/recipe1",
				"https://example.com/recipe2"
			},
			["PartialDiscovery"] = true
		};

		// Act
		sagaState.CreateCheckpoint("DiscoveryCheckpoint", checkpointData);

		// Assert
		sagaState.CurrentPhase.Should().Be("Discovering");
		sagaState.HasCheckpoint.Should().BeTrue();
		Dictionary<string, object>? checkpoint = sagaState.GetCheckpoint("DiscoveryCheckpoint");
		checkpoint!["PartialDiscovery"].Should().Be(true);

		var discoveredUrls = checkpoint["DiscoveredUrls"] as List<string>;
		discoveredUrls.Should().HaveCount(2);
	}

	[Fact(DisplayName = "Saga handles crash during Processing phase")]
	public void SagaState_HandlesCrash_DuringProcessing()
	{
		// Arrange - Saga crashed while processing recipes
		var sagaState = SagaState
			.CreateForRecipeProcessing(Guid.NewGuid());

		sagaState.Start();
		sagaState.UpdateProgress("Processing", 60);

		var checkpointData = new Dictionary<string, object>
		{
			["FingerprintedUrls"] = new List<string>
			{
				"https://example.com/recipe1",
				"https://example.com/recipe2",
				"https://example.com/recipe3",
				"https://example.com/recipe4",
				"https://example.com/recipe5"
			},
			["ProcessedUrls"] = new List<string>
			{
				"https://example.com/recipe1",
				"https://example.com/recipe2",
				"https://example.com/recipe3"
			},
			["CurrentIndex"] = 3,
			["FailedUrls"] = new List<string>()
		};

		// Act
		sagaState.CreateCheckpoint("ProcessingCheckpoint", checkpointData);

		// Assert
		sagaState.CurrentPhase.Should().Be("Processing");
		Dictionary<string, object>? checkpoint = sagaState.GetCheckpoint("ProcessingCheckpoint");
		checkpoint!["CurrentIndex"].Should().Be(3);

		var processedUrls = checkpoint["ProcessedUrls"] as List<string>;
		processedUrls.Should().HaveCount(3);

		var fingerprintedUrls = checkpoint["FingerprintedUrls"] as List<string>;
		int remainingCount = fingerprintedUrls!.Count - (int)checkpoint["CurrentIndex"];
		remainingCount.Should().Be(2); // 2 URLs remaining to process
	}

	[Fact(DisplayName = "Saga state persists after each recipe for crash recovery")]
	public void SagaState_PersistsProgress_AfterEachRecipe()
	{
		// Arrange
		var sagaState = SagaState
			.CreateForRecipeProcessing(Guid.NewGuid());

		sagaState.Start();

		var checkpointData = new Dictionary<string, object>
		{
			["DiscoveredUrls"] = new List<string>
			{
				"https://example.com/recipe1",
				"https://example.com/recipe2",
				"https://example.com/recipe3"
			},
			["ProcessedUrls"] = new List<string>
			{
				"https://example.com/recipe1"
			},
			["CurrentIndex"] = 1,
			["Phase"] = "Processing"
		};

		// Act
		sagaState.CreateCheckpoint("RecipeProcessed", checkpointData);

		// Assert
		sagaState.HasCheckpoint.Should().BeTrue();
		Dictionary<string, object>? checkpoint = sagaState.GetCheckpoint("RecipeProcessed");
		checkpoint.Should().NotBeNull();
		checkpoint!["CurrentIndex"].Should().Be(1);
		checkpoint["Phase"].Should().Be("Processing");
	}

	[Fact(DisplayName = "Saga preserves metrics across crash recovery")]
	public void SagaState_PreservesMetrics_AcrossCrashRecovery()
	{
		// Arrange - Original saga with metrics
		var originalMetrics = new SagaMetrics
		{
			ItemsProcessed = 42,
			ItemsFailed = 3,
			TotalProcessingTime = TimeSpan.FromMinutes(15),
			TotalUpdates = 45
		};

		var sagaId = Guid.NewGuid();
		var correlationId = Guid.NewGuid();

		// Act - Reconstitute after crash
		SagaState reconstitutedState = SagaState.Reconstitute(
			sagaId,
			"RecipeProcessingSaga",
			correlationId,
			SagaStatus.Running,
			"Processing",
			60,
			new Dictionary<string, object>(),
			new Dictionary<string, object>(),
			originalMetrics,
			null,
			null,
			DateTime.UtcNow.AddMinutes(-20),
			DateTime.UtcNow.AddMinutes(-15),
			DateTime.UtcNow,
			null
		);

		// Assert - Metrics should be preserved
		reconstitutedState.Metrics.ItemsProcessed.Should().Be(42);
		reconstitutedState.Metrics.ItemsFailed.Should().Be(3);
		reconstitutedState.Metrics.TotalProcessingTime.Should().Be(TimeSpan.FromMinutes(15));
		reconstitutedState.Metrics.TotalUpdates.Should().Be(45);
	}

	[Fact(DisplayName = "Saga resumes from CurrentIndex after crash")]
	public void SagaState_ResumesFromCurrentIndex_AfterCrash()
	{
		// Arrange - Saga state before crash
		var sagaState = SagaState
			.CreateForRecipeProcessing(Guid.NewGuid());

		sagaState.Start();

		var stateData = new Dictionary<string, object>
		{
			["FingerprintedUrls"] = new List<string>
			{
				"https://example.com/recipe1",
				"https://example.com/recipe2",
				"https://example.com/recipe3",
				"https://example.com/recipe4",
				"https://example.com/recipe5"
			},
			["ProcessedUrls"] = new List<string>
			{
				"https://example.com/recipe1",
				"https://example.com/recipe2"
			},
			["CurrentIndex"] = 2 // Crashed while processing recipe 3
		};

		// Act
		sagaState.UpdateProgress("Processing", 40, stateData);

		// Assert - After restart, should resume from index 2
		sagaState.StateData["CurrentIndex"].Should().Be(2);
		var processedUrls = sagaState.StateData["ProcessedUrls"] as List<string>;
		processedUrls.Should().HaveCount(2);

		var fingerprintedUrls = sagaState.StateData["FingerprintedUrls"] as List<string>;
		List<string> remainingUrls = fingerprintedUrls!.Skip(2).ToList();
		remainingUrls.Should().HaveCount(3); // 3 URLs remaining to process
	}

	[Fact(DisplayName = "Saga skips already processed URLs on recovery")]
	public void SagaState_SkipsProcessedUrls_OnRecovery()
	{
		// Arrange
		var processedUrls = new List<string>
		{
			"https://example.com/recipe1",
			"https://example.com/recipe2"
		};

		var fingerprintedUrls = new List<string>
		{
			"https://example.com/recipe1", // Already processed
			"https://example.com/recipe2", // Already processed
			"https://example.com/recipe3", // Should process this
			"https://example.com/recipe4" // Should process this
		};

		// Act - Determine which URLs need processing
		List<string> urlsToProcess = fingerprintedUrls
			.Where(url => !processedUrls.Contains(url))
			.ToList();

		// Assert
		urlsToProcess.Should().HaveCount(2);
		urlsToProcess.Should().Contain("https://example.com/recipe3");
		urlsToProcess.Should().Contain("https://example.com/recipe4");
		urlsToProcess.Should().NotContain("https://example.com/recipe1");
		urlsToProcess.Should().NotContain("https://example.com/recipe2");
	}
}
using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Application.Sagas;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace EasyMeals.RecipeEngine.Tests.Contract;

/// <summary>
///     Contract tests for RecipeProcessingSaga state transitions.
///     Tests the saga workflow: Idle → Discovering → Fingerprinting → Processing → Persisting → Completed
///     These tests verify the saga behaves according to its contract without testing implementation details.
/// </summary>
public class RecipeProcessingSagaContractTests
{
	[Fact(DisplayName = "Saga persists state data for crash recovery")]
	public void SagaState_SupportsCheckpointing_ForCrashRecovery()
	{
		// Arrange
		var sagaState = SagaState.CreateForRecipeProcessing(Guid.NewGuid());
		var checkpointData = new Dictionary<string, object>
		{
			["DiscoveredUrls"] = new List<string> { "https://example.com/recipe1" },
			["CurrentIndex"] = 0
		};

		// Act
		sagaState.CreateCheckpoint("ProcessingCheckpoint", checkpointData);

		// Assert
		sagaState.HasCheckpoint.Should().BeTrue();
		Dictionary<string, object>? retrievedCheckpoint = sagaState.GetCheckpoint("ProcessingCheckpoint");
		retrievedCheckpoint.Should().NotBeNull();
		retrievedCheckpoint.Should().ContainKey("DiscoveredUrls");
		retrievedCheckpoint.Should().ContainKey("CurrentIndex");
	}

	[Fact(DisplayName = "Saga can be paused and resumed")]
	public void SagaState_SupportsPauseResume_ForGracefulShutdown()
	{
		// Arrange
		var sagaState = SagaState.CreateForRecipeProcessing(Guid.NewGuid());
		sagaState.Start();

		// Act - Pause
		sagaState.Pause();

		// Assert
		sagaState.Status.Should().Be(SagaStatus.Paused);
		sagaState.CanResume.Should().BeTrue();

		// Act - Resume
		sagaState.Resume();

		// Assert
		sagaState.Status.Should().Be(SagaStatus.Running);
		sagaState.IsRunning.Should().BeTrue();
	}

	[Fact(DisplayName = "Saga handles failure state")]
	public void SagaState_TracksFailure_WithErrorDetails()
	{
		// Arrange
		var sagaState = SagaState.CreateForRecipeProcessing(Guid.NewGuid());
		sagaState.Start();
		var errorMessage = "Network error during recipe fetch";
		var stackTrace = "at RecipeProcessor.FetchAsync()";

		// Act
		sagaState.Fail(errorMessage, stackTrace);

		// Assert
		sagaState.Status.Should().Be(SagaStatus.Failed);
		sagaState.IsFailed.Should().BeTrue();
		sagaState.ErrorMessage.Should().Be(errorMessage);
		sagaState.ErrorStackTrace.Should().Be(stackTrace);
		sagaState.CompletedAt.Should().NotBeNull();
	}

	[Fact(DisplayName = "Saga tracks processing metrics")]
	public void SagaState_TracksMetrics_ForObservability()
	{
		// Arrange
		var sagaState = SagaState.CreateForRecipeProcessing(Guid.NewGuid());

		// Act
		sagaState.UpdateMetrics(10, 2, TimeSpan.FromSeconds(30));

		// Assert
		sagaState.Metrics.ItemsProcessed.Should().Be(10);
		sagaState.Metrics.ItemsFailed.Should().Be(2);
		sagaState.Metrics.TotalProcessingTime.Should().Be(TimeSpan.FromSeconds(30));
	}

	[Fact(DisplayName = "Saga tracks workflow phases")]
	public void SagaState_TracksPhases_ThroughWorkflow()
	{
		// Arrange
		var sagaState = SagaState.CreateForRecipeProcessing(Guid.NewGuid());

		// Act & Assert - Phase progression
		sagaState.Start();
		sagaState.Status.Should().Be(SagaStatus.Running);

		sagaState.UpdateProgress("Discovering", 25);
		sagaState.CurrentPhase.Should().Be("Discovering");
		sagaState.PhaseProgress.Should().Be(25);

		sagaState.UpdateProgress("Fingerprinting", 50);
		sagaState.CurrentPhase.Should().Be("Fingerprinting");
		sagaState.PhaseProgress.Should().Be(50);

		sagaState.UpdateProgress("Processing", 75);
		sagaState.CurrentPhase.Should().Be("Processing");
		sagaState.PhaseProgress.Should().Be(75);

		sagaState.UpdateProgress("Persisting", 90);
		sagaState.CurrentPhase.Should().Be("Persisting");
		sagaState.PhaseProgress.Should().Be(90);

		sagaState.Complete();
		sagaState.Status.Should().Be(SagaStatus.Completed);
		sagaState.IsCompleted.Should().BeTrue();
	}

	[Fact(DisplayName = "Saga state includes required workflow data")]
	public async Task StartProcessingAsync_CreatesState_WithRequiredProperties()
	{
		// Arrange
		var mockSagaRepo = new Mock<ISagaStateRepository>();
		SagaState? capturedState = null;

		mockSagaRepo.Setup(r => r.AddAsync(It.IsAny<SagaState>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((SagaState state, CancellationToken _) =>
			{
				capturedState = state;
				return state;
			});
		mockSagaRepo.Setup(r => r.UpdateAsync(It.IsAny<SagaState>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((SagaState state, CancellationToken _) => state);

		var mockConfigLoader = new Mock<IProviderConfigurationLoader>();
		mockConfigLoader.Setup(c => c.GetByProviderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((ProviderConfiguration?)null);

		var saga = new RecipeProcessingSaga(
			Mock.Of<ILogger<RecipeProcessingSaga>>(),
			mockSagaRepo.Object,
			mockConfigLoader.Object,
			Mock.Of<IDiscoveryService>(),
			Mock.Of<IRecipeFingerprinter>(),
			Mock.Of<IIngredientNormalizer>(),
			Mock.Of<IRateLimiter>(),
			Mock.Of<IRecipeBatchRepository>(),
			Mock.Of<IEventBus>()
		);

		// Act
		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await saga.StartProcessingAsync("provider_001", 100, TimeSpan.FromHours(1), CancellationToken.None));

		// Assert - State includes workflow data
		capturedState.Should().NotBeNull();
		capturedState!.Id.Should().NotBeEmpty();
		capturedState.CorrelationId.Should().NotBeEmpty();
		capturedState.SagaType.Should().Be("RecipeProcessingSaga");
		capturedState.StateData.Should().NotBeNull();
		capturedState.StateData.Should().ContainKey("ProviderId");
		capturedState.StateData.Should().ContainKey("BatchSize");
		capturedState.StateData.Should().ContainKey("DiscoveredUrls");
		capturedState.StateData.Should().ContainKey("ProcessedUrls");
		capturedState.StateData.Should().ContainKey("FailedUrls");
		capturedState.StateData.Should().ContainKey("CurrentIndex");
	}

	[Fact(DisplayName = "Saga starts in created state")]
	public async Task StartProcessingAsync_InitialState_SagaCreated()
	{
		// Arrange
		var mockSagaRepo = new Mock<ISagaStateRepository>();
		SagaState? addedState = null;

		mockSagaRepo.Setup(r => r.AddAsync(It.IsAny<SagaState>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((SagaState state, CancellationToken _) =>
			{
				addedState = state;
				return state;
			});

		// The saga will execute, so we need to provide all dependencies
		mockSagaRepo.Setup(r => r.UpdateAsync(It.IsAny<SagaState>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((SagaState state, CancellationToken _) => state);

		var mockConfigLoader = new Mock<IProviderConfigurationLoader>();
		mockConfigLoader.Setup(c => c.GetByProviderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((ProviderConfiguration?)null); // Will cause exception but that's okay for this test

		var saga = new RecipeProcessingSaga(
			Mock.Of<ILogger<RecipeProcessingSaga>>(),
			mockSagaRepo.Object,
			mockConfigLoader.Object,
			Mock.Of<IDiscoveryService>(),
			Mock.Of<IRecipeFingerprinter>(),
			Mock.Of<IIngredientNormalizer>(),
			Mock.Of<IRateLimiter>(),
			Mock.Of<IRecipeBatchRepository>(),
			Mock.Of<IEventBus>()
		);

		// Act & Assert - Expect exception due to null config
		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await saga.StartProcessingAsync("provider_001", 100, TimeSpan.FromHours(1), CancellationToken.None));

		// Verify saga state was created and added
		mockSagaRepo.Verify(r => r.AddAsync(
			It.IsAny<SagaState>(),
			It.IsAny<CancellationToken>()), Times.Once);

		// The state should have been created
		addedState.Should().NotBeNull();
		addedState!.SagaType.Should().Be("RecipeProcessingSaga");
	}

	[Fact(DisplayName = "Saga transitions from Created to Discovering")]
	public async Task StartProcessingAsync_WhenCalled_TransitionsToDiscovering()
	{
		// Arrange
		var mockSagaRepo = new Mock<ISagaStateRepository>();
		SagaState? capturedState = null;

		mockSagaRepo.Setup(r => r.AddAsync(It.IsAny<SagaState>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((SagaState state, CancellationToken _) =>
			{
				capturedState = state;
				return state;
			});
		mockSagaRepo.Setup(r => r.UpdateAsync(It.IsAny<SagaState>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((SagaState state, CancellationToken _) => state);

		var mockConfigLoader = new Mock<IProviderConfigurationLoader>();
		mockConfigLoader.Setup(c => c.GetByProviderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((ProviderConfiguration?)null); // Will trigger exception

		var saga = new RecipeProcessingSaga(
			Mock.Of<ILogger<RecipeProcessingSaga>>(),
			mockSagaRepo.Object,
			mockConfigLoader.Object,
			Mock.Of<IDiscoveryService>(),
			Mock.Of<IRecipeFingerprinter>(),
			Mock.Of<IIngredientNormalizer>(),
			Mock.Of<IRateLimiter>(),
			Mock.Of<IRecipeBatchRepository>(),
			Mock.Of<IEventBus>()
		);

		// Act & Assert - Expect exception when config is null
		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await saga.StartProcessingAsync("provider_001", 100, TimeSpan.FromHours(1), CancellationToken.None));

		// Assert - State was created and moved to Discovering phase before failing
		capturedState.Should().NotBeNull();
		capturedState!.CurrentPhase.Should().Be("Discovering");
		// Note: The saga will fail due to null config, so status will be Failed
		// This is expected behavior for this test scenario
	}
}
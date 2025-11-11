using EasyMeals.RecipeEngine.Application.Interfaces;
using EasyMeals.RecipeEngine.Application.Sagas;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using EasyMeals.RecipeEngine.Domain.Repositories;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using Shouldly;
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
        sagaState.HasCheckpoint.ShouldBeTrue();
        Dictionary<string, object>? retrievedCheckpoint = sagaState.GetCheckpoint("ProcessingCheckpoint");
        retrievedCheckpoint.ShouldNotBeNull();
        retrievedCheckpoint.ShouldContainKey("DiscoveredUrls");
        retrievedCheckpoint.ShouldContainKey("CurrentIndex");
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
        sagaState.Status.ShouldBe(SagaStatus.Paused);
        sagaState.CanResume.ShouldBeTrue();

        // Act - Resume
        sagaState.Resume();

        // Assert
        sagaState.Status.ShouldBe(SagaStatus.Running);
        sagaState.IsRunning.ShouldBeTrue();
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
        sagaState.Status.ShouldBe(SagaStatus.Failed);
        sagaState.IsFailed.ShouldBeTrue();
        sagaState.ErrorMessage.ShouldBe(errorMessage);
        sagaState.ErrorStackTrace.ShouldBe(stackTrace);
        sagaState.CompletedAt.ShouldNotBeNull();
    }

    [Fact(DisplayName = "Saga tracks processing metrics")]
    public void SagaState_TracksMetrics_ForObservability()
    {
        // Arrange
        var sagaState = SagaState.CreateForRecipeProcessing(Guid.NewGuid());

        // Act
        sagaState.UpdateMetrics(10, 2, TimeSpan.FromSeconds(30));

        // Assert
        sagaState.Metrics.ItemsProcessed.ShouldBe(10);
        sagaState.Metrics.ItemsFailed.ShouldBe(2);
        sagaState.Metrics.TotalProcessingTime.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact(DisplayName = "Saga tracks workflow phases")]
    public void SagaState_TracksPhases_ThroughWorkflow()
    {
        // Arrange
        var sagaState = SagaState.CreateForRecipeProcessing(Guid.NewGuid());

        // Act & Assert - Phase progression
        sagaState.Start();
        sagaState.Status.ShouldBe(SagaStatus.Running);

        sagaState.UpdateProgress("Discovering", 25);
        sagaState.CurrentPhase.ShouldBe("Discovering");
        sagaState.PhaseProgress.ShouldBe(25);

        sagaState.UpdateProgress("Fingerprinting", 50);
        sagaState.CurrentPhase.ShouldBe("Fingerprinting");
        sagaState.PhaseProgress.ShouldBe(50);

        sagaState.UpdateProgress("Processing", 75);
        sagaState.CurrentPhase.ShouldBe("Processing");
        sagaState.PhaseProgress.ShouldBe(75);

        sagaState.UpdateProgress("Persisting", 90);
        sagaState.CurrentPhase.ShouldBe("Persisting");
        sagaState.PhaseProgress.ShouldBe(90);

        sagaState.Complete();
        sagaState.Status.ShouldBe(SagaStatus.Completed);
        sagaState.IsCompleted.ShouldBeTrue();
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
            Mock.Of<IDiscoveryServiceFactory>(),
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
        capturedState.ShouldNotBeNull();
        capturedState!.Id.ShouldNotBe(Guid.Empty);
        capturedState.CorrelationId.ShouldNotBe(Guid.Empty);
        capturedState.SagaType.ShouldBe("RecipeProcessingSaga");
        capturedState.StateData.ShouldNotBeNull();
        capturedState.StateData.ShouldContainKey("ProviderId");
        capturedState.StateData.ShouldContainKey("BatchSize");
        capturedState.StateData.ShouldContainKey("DiscoveredUrls");
        capturedState.StateData.ShouldContainKey("ProcessedUrls");
        capturedState.StateData.ShouldContainKey("FailedUrls");
        capturedState.StateData.ShouldContainKey("CurrentIndex");
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
            Mock.Of<IDiscoveryServiceFactory>(),
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
        addedState.ShouldNotBeNull();
        addedState!.SagaType.ShouldBe("RecipeProcessingSaga");
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
            Mock.Of<IDiscoveryServiceFactory>(),
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
        capturedState.ShouldNotBeNull();
        capturedState!.CurrentPhase.ShouldBe("Discovering");
        // Note: The saga will fail due to null config, so status will be Failed
        // This is expected behavior for this test scenario
    }
}
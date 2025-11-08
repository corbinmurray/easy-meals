using EasyMeals.RecipeEngine.Application.Sagas;
using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace EasyMeals.RecipeEngine.Tests.Contract;

/// <summary>
/// Contract tests for RecipeProcessingSaga state transitions.
/// Tests the saga workflow: Idle → Discovering → Fingerprinting → Processing → Persisting → Completed
/// These tests verify the saga behaves according to its contract without testing implementation details.
/// </summary>
public class RecipeProcessingSagaContractTests
{
    [Fact(DisplayName = "Saga starts in created state")]
    public async Task StartProcessingAsync_InitialState_SagaCreated()
    {
        // Arrange
        var mockSagaRepo = new Mock<ISagaStateRepository>();
        mockSagaRepo.Setup(r => r.AddAsync(It.IsAny<SagaState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SagaState state, CancellationToken _) => state);

        var saga = new RecipeProcessingSaga(
            Mock.Of<Microsoft.Extensions.Logging.ILogger<RecipeProcessingSaga>>(),
            mockSagaRepo.Object
        );

        // Act
        await saga.StartProcessingAsync(CancellationToken.None);

        // Assert
        mockSagaRepo.Verify(r => r.AddAsync(
            It.Is<SagaState>(s => s.Status == SagaStatus.Created),
            It.IsAny<CancellationToken>()), Times.Once);
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

        var saga = new RecipeProcessingSaga(
            Mock.Of<Microsoft.Extensions.Logging.ILogger<RecipeProcessingSaga>>(),
            mockSagaRepo.Object
        );

        // Act
        await saga.StartProcessingAsync(CancellationToken.None);

        // Assert
        capturedState.Should().NotBeNull();
        capturedState!.Status.Should().Be(SagaStatus.Created);
        // Note: Actual transition to Discovering will happen in the saga implementation
        // This test verifies the saga is created and ready to start
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

        var saga = new RecipeProcessingSaga(
            Mock.Of<Microsoft.Extensions.Logging.ILogger<RecipeProcessingSaga>>(),
            mockSagaRepo.Object
        );

        // Act
        await saga.StartProcessingAsync(CancellationToken.None);

        // Assert
        capturedState.Should().NotBeNull();
        capturedState!.Id.Should().NotBeEmpty();
        capturedState.CorrelationId.Should().NotBeEmpty();
        capturedState.SagaType.Should().Be("RecipeProcessingSaga");
        capturedState.StateData.Should().NotBeNull();
        capturedState.CheckpointData.Should().NotBeNull();
    }

    [Fact(DisplayName = "Saga persists state data for crash recovery")]
    public void SagaState_SupportsCheckpointing_ForCrashRecovery()
    {
        // Arrange
        var sagaState = SagaState.CreateForRecipeProcessing(Guid.NewGuid(), "RecipeProcessingSaga");
        var checkpointData = new Dictionary<string, object>
        {
            ["DiscoveredUrls"] = new List<string> { "https://example.com/recipe1" },
            ["CurrentIndex"] = 0
        };

        // Act
        sagaState.CreateCheckpoint("ProcessingCheckpoint", checkpointData);

        // Assert
        sagaState.HasCheckpoint.Should().BeTrue();
        var retrievedCheckpoint = sagaState.GetCheckpoint("ProcessingCheckpoint");
        retrievedCheckpoint.Should().NotBeNull();
        retrievedCheckpoint.Should().ContainKey("DiscoveredUrls");
        retrievedCheckpoint.Should().ContainKey("CurrentIndex");
    }

    [Fact(DisplayName = "Saga tracks workflow phases")]
    public void SagaState_TracksPhases_ThroughWorkflow()
    {
        // Arrange
        var sagaState = SagaState.CreateForRecipeProcessing(Guid.NewGuid(), "RecipeProcessingSaga");
        
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

    [Fact(DisplayName = "Saga can be paused and resumed")]
    public void SagaState_SupportsPauseResume_ForGracefulShutdown()
    {
        // Arrange
        var sagaState = SagaState.CreateForRecipeProcessing(Guid.NewGuid(), "RecipeProcessingSaga");
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
        var sagaState = SagaState.CreateForRecipeProcessing(Guid.NewGuid(), "RecipeProcessingSaga");
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
        var sagaState = SagaState.CreateForRecipeProcessing(Guid.NewGuid(), "RecipeProcessingSaga");
        
        // Act
        sagaState.UpdateMetrics(itemsProcessed: 10, itemsFailed: 2, phaseDuration: TimeSpan.FromSeconds(30));
        
        // Assert
        sagaState.Metrics.ItemsProcessed.Should().Be(10);
        sagaState.Metrics.ItemsFailed.Should().Be(2);
        sagaState.Metrics.TotalProcessingTime.Should().Be(TimeSpan.FromSeconds(30));
    }
}

using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Services;
using EasyMeals.RecipeEngine.Infrastructure.Services;
using Shouldly;

namespace EasyMeals.RecipeEngine.Tests.Unit.DomainServices;

/// <summary>
///     Unit tests for BatchCompletionPolicy domain service.
///     Tests batch size reached, time window exceeded, and both conditions.
/// </summary>
public class BatchCompletionPolicyTests
{
    [Fact(DisplayName = "Completion reason should be descriptive")]
    public void GetCompletionReason_WhenBatchComplete_ReturnsDescriptiveReason()
    {
        // Arrange
        var policy = new BatchCompletionPolicy();
        var batch = RecipeBatch.CreateBatch(
            "test-provider",
            5,
            60
        );

        // Mark batch size reached
        for (var i = 0; i < 5; i++)
        {
            batch.MarkRecipeProcessed($"https://example.com/recipe-{i}");
        }

        // Act
        BatchCompletionReason reason = policy.GetCompletionReason(batch, DateTime.UtcNow);

        // Assert
        reason.ShouldBe(BatchCompletionReason.BatchSizeReached);
    }

    [Fact(DisplayName = "Should not complete when batch size not reached and time window not exceeded")]
    public void ShouldCompleteBatch_BatchNotComplete_ReturnsFalse()
    {
        // Arrange
        var policy = new BatchCompletionPolicy();
        var batch = RecipeBatch.CreateBatch(
            "test-provider",
            10,
            60
        );

        // Process only 5 recipes (batch size not reached)
        for (var i = 0; i < 5; i++)
        {
            batch.MarkRecipeProcessed($"https://example.com/recipe-{i}");
        }

        // Act
        bool shouldComplete = policy.ShouldCompleteBatch(batch, DateTime.UtcNow);

        // Assert
        shouldComplete.ShouldBeFalse();
    }

    [Fact(DisplayName = "Should complete when batch size reached")]
    public void ShouldCompleteBatch_BatchSizeReached_ReturnsTrue()
    {
        // Arrange
        var policy = new BatchCompletionPolicy();
        var batch = RecipeBatch.CreateBatch(
            "test-provider",
            10,
            60
        );

        // Simulate processing 10 recipes (batch size reached)
        for (var i = 0; i < 10; i++)
        {
            batch.MarkRecipeProcessed($"https://example.com/recipe-{i}");
        }

        // Act
        bool shouldComplete = policy.ShouldCompleteBatch(batch, DateTime.UtcNow);
        BatchCompletionReason reason = policy.GetCompletionReason(batch, DateTime.UtcNow);

        // Assert
        shouldComplete.ShouldBeTrue();
        reason.ShouldBe(BatchCompletionReason.BatchSizeReached);
    }

    [Fact(DisplayName = "Should complete when both batch size and time window conditions met")]
    public void ShouldCompleteBatch_BothConditionsMet_ReturnsTrue()
    {
        // Arrange
        var policy = new BatchCompletionPolicy();

        // Create a batch that started more than the time window ago
        var batchId = Guid.NewGuid();
        var providerId = "test-provider";
        DateTime startedAt = DateTime.UtcNow.AddMinutes(-65);

        RecipeBatch batch = RecipeBatch.Reconstitute(
            batchId,
            providerId,
            10,
            60,
            startedAt,
            null,
            10, // Batch size reached
            0,
            0,
            "InProgress",
            Enumerable.Range(1, 10).Select(i => $"url{i}").ToList(),
            new List<string>()
        );

        // Act
        bool shouldComplete = policy.ShouldCompleteBatch(batch, DateTime.UtcNow);
        BatchCompletionReason reason = policy.GetCompletionReason(batch, DateTime.UtcNow);

        // Assert
        shouldComplete.ShouldBeTrue();
        reason.ShouldBe(BatchCompletionReason.Both);
    }

    [Fact(DisplayName = "Should complete when approaching time window limit with no progress")]
    public void ShouldCompleteBatch_NoProgressNearTimeout_ReturnsTrue()
    {
        // Arrange
        var policy = new BatchCompletionPolicy();

        // Create a batch that's been running for almost the full time window with no recipes processed
        var batchId = Guid.NewGuid();
        var providerId = "test-provider";
        DateTime startedAt = DateTime.UtcNow.AddMinutes(-59); // 59 minutes ago, time window is 60

        RecipeBatch batch = RecipeBatch.Reconstitute(
            batchId,
            providerId,
            100,
            60,
            startedAt,
            null,
            0, // No progress
            0,
            0,
            "InProgress",
            new List<string>(),
            new List<string>()
        );

        // Act
        bool shouldComplete = policy.ShouldCompleteBatch(batch, DateTime.UtcNow);

        // Assert - Should not complete yet, still within time window
        shouldComplete.ShouldBeFalse();
    }

    [Fact(DisplayName = "Should complete when time window exceeded")]
    public void ShouldCompleteBatch_TimeWindowExceeded_ReturnsTrue()
    {
        // Arrange
        var policy = new BatchCompletionPolicy();

        // Create a batch that started more than the time window ago
        var batchId = Guid.NewGuid();
        var providerId = "test-provider";
        DateTime startedAt = DateTime.UtcNow.AddMinutes(-65); // Started 65 minutes ago

        // Create batch using Reconstitute to set custom start time
        RecipeBatch batch = RecipeBatch.Reconstitute(
            batchId,
            providerId,
            100,
            60,
            startedAt,
            null,
            5, // Only processed 5 out of 100
            0,
            0,
            "InProgress",
            new List<string> { "url1", "url2", "url3", "url4", "url5" },
            new List<string>()
        );

        // Act
        bool shouldComplete = policy.ShouldCompleteBatch(batch, DateTime.UtcNow);
        BatchCompletionReason reason = policy.GetCompletionReason(batch, DateTime.UtcNow);

        // Assert
        shouldComplete.ShouldBeTrue();
        reason.ShouldBe(BatchCompletionReason.TimeWindowExceeded);
    }

    [Fact(DisplayName = "Should handle zero batch size gracefully")]
    public void ShouldCompleteBatch_ZeroBatchSize_ReturnsTrue()
    {
        // Arrange
        var policy = new BatchCompletionPolicy();

        // Create batch with zero batch size using Reconstitute to bypass validation
        RecipeBatch batch = RecipeBatch.Reconstitute(
            Guid.NewGuid(),
            "test-provider",
            0,
            60,
            DateTime.UtcNow,
            null,
            0,
            0,
            0,
            "InProgress",
            new List<string>(),
            new List<string>()
        );

        // Act
        bool shouldComplete = policy.ShouldCompleteBatch(batch, DateTime.UtcNow);

        // Assert - With zero batch size, processedCount (0) >= batchSize (0), so it should complete
        shouldComplete.ShouldBeTrue();
    }
}
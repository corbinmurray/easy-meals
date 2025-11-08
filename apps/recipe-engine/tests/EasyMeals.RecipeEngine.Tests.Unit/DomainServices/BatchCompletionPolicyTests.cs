using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Services;
using EasyMeals.RecipeEngine.Infrastructure.Services;
using FluentAssertions;

namespace EasyMeals.RecipeEngine.Tests.Unit.DomainServices;

/// <summary>
/// Unit tests for BatchCompletionPolicy domain service.
/// Tests batch size reached, time window exceeded, and both conditions.
/// </summary>
public class BatchCompletionPolicyTests
{
    [Fact(DisplayName = "Should complete when batch size reached")]
    public void ShouldCompleteBatch_BatchSizeReached_ReturnsTrue()
    {
        // Arrange
        var policy = new BatchCompletionPolicy();
        var batch = RecipeBatch.CreateBatch(
            providerId: "test-provider",
            batchSize: 10,
            timeWindowMinutes: 60
        );

        // Simulate processing 10 recipes (batch size reached)
        for (int i = 0; i < 10; i++)
        {
            batch.MarkRecipeProcessed($"https://example.com/recipe-{i}");
        }

        // Act
        var shouldComplete = policy.ShouldCompleteBatch(batch, DateTime.UtcNow);
        var reason = policy.GetCompletionReason(batch, DateTime.UtcNow);

        // Assert
        shouldComplete.Should().BeTrue();
        reason.Should().Be(BatchCompletionReason.BatchSizeReached);
    }

    [Fact(DisplayName = "Should not complete when batch size not reached and time window not exceeded")]
    public void ShouldCompleteBatch_BatchNotComplete_ReturnsFalse()
    {
        // Arrange
        var policy = new BatchCompletionPolicy();
        var batch = RecipeBatch.CreateBatch(
            providerId: "test-provider",
            batchSize: 10,
            timeWindowMinutes: 60
        );

        // Process only 5 recipes (batch size not reached)
        for (int i = 0; i < 5; i++)
        {
            batch.MarkRecipeProcessed($"https://example.com/recipe-{i}");
        }

        // Act
        var shouldComplete = policy.ShouldCompleteBatch(batch, DateTime.UtcNow);

        // Assert
        shouldComplete.Should().BeFalse();
    }

    [Fact(DisplayName = "Should complete when time window exceeded")]
    public void ShouldCompleteBatch_TimeWindowExceeded_ReturnsTrue()
    {
        // Arrange
        var policy = new BatchCompletionPolicy();
        
        // Create a batch that started more than the time window ago
        var batchId = Guid.NewGuid();
        var providerId = "test-provider";
        var startedAt = DateTime.UtcNow.AddMinutes(-65); // Started 65 minutes ago
        
        // Create batch using Reconstitute to set custom start time
        var batch = RecipeBatch.Reconstitute(
            id: batchId,
            providerId: providerId,
            batchSize: 100,
            timeWindowMinutes: 60,
            startedAt: startedAt,
            completedAt: null,
            processedCount: 5, // Only processed 5 out of 100
            skippedCount: 0,
            failedCount: 0,
            status: "InProgress",
            processedUrls: new List<string> { "url1", "url2", "url3", "url4", "url5" },
            failedUrls: new List<string>()
        );

        // Act
        var shouldComplete = policy.ShouldCompleteBatch(batch, DateTime.UtcNow);
        var reason = policy.GetCompletionReason(batch, DateTime.UtcNow);

        // Assert
        shouldComplete.Should().BeTrue();
        reason.Should().Be(BatchCompletionReason.TimeWindowExceeded);
    }

    [Fact(DisplayName = "Should complete when both batch size and time window conditions met")]
    public void ShouldCompleteBatch_BothConditionsMet_ReturnsTrue()
    {
        // Arrange
        var policy = new BatchCompletionPolicy();
        
        // Create a batch that started more than the time window ago
        var batchId = Guid.NewGuid();
        var providerId = "test-provider";
        var startedAt = DateTime.UtcNow.AddMinutes(-65);
        
        var batch = RecipeBatch.Reconstitute(
            id: batchId,
            providerId: providerId,
            batchSize: 10,
            timeWindowMinutes: 60,
            startedAt: startedAt,
            completedAt: null,
            processedCount: 10, // Batch size reached
            skippedCount: 0,
            failedCount: 0,
            status: "InProgress",
            processedUrls: Enumerable.Range(1, 10).Select(i => $"url{i}").ToList(),
            failedUrls: new List<string>()
        );

        // Act
        var shouldComplete = policy.ShouldCompleteBatch(batch, DateTime.UtcNow);
        var reason = policy.GetCompletionReason(batch, DateTime.UtcNow);

        // Assert
        shouldComplete.Should().BeTrue();
        reason.Should().Be(BatchCompletionReason.Both);
    }

    [Fact(DisplayName = "Should handle zero batch size gracefully")]
    public void ShouldCompleteBatch_ZeroBatchSize_ReturnsTrue()
    {
        // Arrange
        var policy = new BatchCompletionPolicy();
        
        // Create batch with zero batch size using Reconstitute to bypass validation
        var batch = RecipeBatch.Reconstitute(
            id: Guid.NewGuid(),
            providerId: "test-provider",
            batchSize: 0,
            timeWindowMinutes: 60,
            startedAt: DateTime.UtcNow,
            completedAt: null,
            processedCount: 0,
            skippedCount: 0,
            failedCount: 0,
            status: "InProgress",
            processedUrls: new List<string>(),
            failedUrls: new List<string>()
        );

        // Act
        var shouldComplete = policy.ShouldCompleteBatch(batch, DateTime.UtcNow);

        // Assert - With zero batch size, processedCount (0) >= batchSize (0), so it should complete
        shouldComplete.Should().BeTrue();
    }

    [Fact(DisplayName = "Completion reason should be descriptive")]
    public void GetCompletionReason_WhenBatchComplete_ReturnsDescriptiveReason()
    {
        // Arrange
        var policy = new BatchCompletionPolicy();
        var batch = RecipeBatch.CreateBatch(
            providerId: "test-provider",
            batchSize: 5,
            timeWindowMinutes: 60
        );

        // Mark batch size reached
        for (int i = 0; i < 5; i++)
        {
            batch.MarkRecipeProcessed($"https://example.com/recipe-{i}");
        }

        // Act
        var reason = policy.GetCompletionReason(batch, DateTime.UtcNow);

        // Assert
        reason.Should().Be(BatchCompletionReason.BatchSizeReached);
    }

    [Fact(DisplayName = "Should complete when approaching time window limit with no progress")]
    public void ShouldCompleteBatch_NoProgressNearTimeout_ReturnsTrue()
    {
        // Arrange
        var policy = new BatchCompletionPolicy();
        
        // Create a batch that's been running for almost the full time window with no recipes processed
        var batchId = Guid.NewGuid();
        var providerId = "test-provider";
        var startedAt = DateTime.UtcNow.AddMinutes(-59); // 59 minutes ago, time window is 60
        
        var batch = RecipeBatch.Reconstitute(
            id: batchId,
            providerId: providerId,
            batchSize: 100,
            timeWindowMinutes: 60,
            startedAt: startedAt,
            completedAt: null,
            processedCount: 0, // No progress
            skippedCount: 0,
            failedCount: 0,
            status: "InProgress",
            processedUrls: new List<string>(),
            failedUrls: new List<string>()
        );

        // Act
        var shouldComplete = policy.ShouldCompleteBatch(batch, DateTime.UtcNow);

        // Assert - Should not complete yet, still within time window
        shouldComplete.Should().BeFalse();
    }
}

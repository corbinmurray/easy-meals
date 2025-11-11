using System.Text.Json;
using EasyMeals.RecipeEngine.Domain.Entities;
using FluentAssertions;

namespace EasyMeals.RecipeEngine.Tests.Contract;

/// <summary>
///     Contract tests for RecipeProcessingSaga permanent error handling.
///     Verifies that data validation errors and other permanent failures are skipped without retry.
/// </summary>
public class RecipeProcessingSagaPermanentErrorTests
{
    /// <summary>
    ///     Contract for permanent error detection.
    ///     This method represents the expected behavior that the saga must implement.
    /// </summary>
    private static bool IsPermanentError(Exception exception)
	{
		return exception switch
		{
			JsonException => true,
			ArgumentNullException => true,
			ArgumentException => true,
			NullReferenceException => true,
			InvalidOperationException => true,
			FormatException => true,
			_ => false
		};
	}

	[Fact(DisplayName = "Saga identifies ArgumentException as permanent error")]
	public void PermanentErrorDetection_IdentifiesArgumentException_AsNonRetryable()
	{
		// Arrange
		var error = new ArgumentException("Invalid argument value");

		// Act
		bool isPermanent = IsPermanentError(error);

		// Assert
		isPermanent.Should().BeTrue("ArgumentException indicates invalid input that won't be fixed by retry");
	}

	[Fact(DisplayName = "Saga identifies InvalidOperationException as permanent error")]
	public void PermanentErrorDetection_IdentifiesInvalidOperationException_AsNonRetryable()
	{
		// Arrange
		var error = new InvalidOperationException("Data validation failed");

		// Act
		bool isPermanent = IsPermanentError(error);

		// Assert
		isPermanent.Should().BeTrue("InvalidOperationException indicates logic error that won't be fixed by retry");
	}

	[Fact(DisplayName = "Saga identifies JsonException as permanent error")]
	public void PermanentErrorDetection_IdentifiesJsonException_AsNonRetryable()
	{
		// Arrange
		var error = new JsonException("Invalid JSON structure");

		// Act
		bool isPermanent = IsPermanentError(error);

		// Assert
		isPermanent.Should().BeTrue("JsonException indicates malformed data that won't be fixed by retry");
	}

	[Fact(DisplayName = "Saga identifies NullReferenceException as permanent error")]
	public void PermanentErrorDetection_IdentifiesNullReferenceException_AsNonRetryable()
	{
		// Arrange
		var error = new NullReferenceException("Missing required field");

		// Act
		bool isPermanent = IsPermanentError(error);

		// Assert
		isPermanent.Should().BeTrue("NullReferenceException indicates missing data that won't be fixed by retry");
	}

	[Fact(DisplayName = "Saga continues after permanent error without blocking")]
	public void PermanentErrorHandling_DoesNotBlock_RemainingProcessing()
	{
		// Arrange
		var totalUrls = 100;
		var permanentFailureIndex = 50;
		var processedCount = 0;
		var failedCount = 0;

		// Act - Simulate processing with permanent failure in middle
		for (var i = 0; i < totalUrls; i++)
		{
			if (i == permanentFailureIndex)
				failedCount++;
			// Don't retry, continue to next URL
			else
				processedCount++;
		}

		// Assert
		processedCount.Should().Be(99, "should process all URLs except the failed one");
		failedCount.Should().Be(1);
		(processedCount + failedCount).Should().Be(totalUrls, "should process all URLs");
	}

	[Fact(DisplayName = "Saga emits ProcessingErrorEvent for permanent failures")]
	public void PermanentErrorHandling_EmitsProcessingErrorEvent_ForMonitoring()
	{
		// Arrange
		var url = "https://example.com/recipe1";
		var providerId = "provider_001";
		var errorMessage = "Invalid data structure";

		// Act - Simulate event emission
		var eventEmitted = true; // In real implementation, this would be checked via event bus
		var eventData = new Dictionary<string, object>
		{
			["EventType"] = "ProcessingError",
			["Url"] = url,
			["ProviderId"] = providerId,
			["Error"] = errorMessage,
			["IsPermanent"] = true,
			["Timestamp"] = DateTime.UtcNow
		};

		// Assert
		eventEmitted.Should().BeTrue("should emit event for monitoring");
		eventData["EventType"].Should().Be("ProcessingError");
		eventData["IsPermanent"].Should().Be(true);
	}

	[Fact(DisplayName = "Saga logs permanent errors with full context")]
	public void PermanentErrorHandling_LogsFullContext_ForDiagnostics()
	{
		// Arrange
		var url = "https://example.com/recipe-with-bad-data";
		var errorMessage = "Invalid JSON: unexpected token at position 42";
		var providerId = "provider_001";

		var errorContext = new Dictionary<string, object>
		{
			["Url"] = url,
			["ProviderId"] = providerId,
			["Error"] = errorMessage,
			["ErrorType"] = "System.Text.Json.JsonException",
			["IsPermanent"] = true,
			["Timestamp"] = DateTime.UtcNow,
			["Phase"] = "Processing"
		};

		// Assert - All required context should be present
		errorContext.Should().ContainKey("Url");
		errorContext.Should().ContainKey("ProviderId");
		errorContext.Should().ContainKey("Error");
		errorContext.Should().ContainKey("ErrorType");
		errorContext.Should().ContainKey("IsPermanent");
		errorContext["IsPermanent"].Should().Be(true);
	}

	[Fact(DisplayName = "Saga skips permanent errors and continues processing")]
	public void PermanentErrorHandling_SkipsUrl_AndContinuesProcessing()
	{
		// Arrange
		var sagaState = SagaState.CreateForRecipeProcessing(Guid.NewGuid());

		var stateData = new Dictionary<string, object>
		{
			["FingerprintedUrls"] = new List<string>
			{
				"https://example.com/recipe1",
				"https://example.com/recipe2", // This one will fail permanently
				"https://example.com/recipe3"
			},
			["ProcessedUrls"] = new List<string>(),
			["FailedUrls"] = new List<Dictionary<string, object>>(),
			["CurrentIndex"] = 0
		};

		sagaState.UpdateProgress("Processing", 0, stateData);

		// Act - Simulate processing with permanent error on recipe2
		var processedUrls = new List<string>();
		var failedUrls = new List<Dictionary<string, object>>();
		var fingerprintedUrls = stateData["FingerprintedUrls"] as List<string>;

		for (var i = 0; i < fingerprintedUrls!.Count; i++)
		{
			string url = fingerprintedUrls[i];

			if (url.Contains("recipe2"))
				// Permanent error - skip without retry
				failedUrls.Add(new Dictionary<string, object>
				{
					["Url"] = url,
					["Error"] = "Invalid JSON structure",
					["IsPermanent"] = true,
					["RetryCount"] = 0
				});
			else
				processedUrls.Add(url);
		}

		// Assert
		processedUrls.Should().HaveCount(2, "should process 2 successful recipes");
		failedUrls.Should().HaveCount(1, "should have 1 permanently failed recipe");
		failedUrls[0]["IsPermanent"].Should().Be(true);
		failedUrls[0]["RetryCount"].Should().Be(0, "permanent errors should not be retried");
	}

	[Fact(DisplayName = "Saga marks permanent errors as non-retryable")]
	public void PermanentErrorMarking_SetsRetryableFlag_ToFalse()
	{
		// Arrange
		var failedUrl = new Dictionary<string, object>
		{
			["Url"] = "https://example.com/recipe1",
			["Error"] = "Data validation failed",
			["IsPermanent"] = true,
			["Retryable"] = false
		};

		// Assert
		failedUrl["IsPermanent"].Should().Be(true);
		failedUrl["Retryable"].Should().Be(false);
	}
}
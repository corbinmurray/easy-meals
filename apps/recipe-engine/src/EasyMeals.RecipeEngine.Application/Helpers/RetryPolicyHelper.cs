using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace EasyMeals.RecipeEngine.Application.Helpers;

/// <summary>
/// Helper class for creating Polly retry policies with exponential backoff and jitter.
/// Provides consistent retry behavior across the application for transient errors.
/// </summary>
public static class RetryPolicyHelper
{
    /// <summary>
    /// Creates an async retry policy with exponential backoff and jitter for transient errors.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="maxRetryAttempts">Maximum number of retry attempts (default: 3)</param>
    /// <param name="baseDelayMs">Base delay in milliseconds for exponential backoff (default: 1000ms)</param>
    /// <param name="logger">Logger for recording retry attempts</param>
    /// <param name="operationName">Name of the operation being retried (for logging)</param>
    /// <returns>A configured async retry policy</returns>
    public static AsyncRetryPolicy<T> CreateRetryPolicy<T>(
        int maxRetryAttempts = 3,
        int baseDelayMs = 1000,
        ILogger? logger = null,
        string operationName = "operation")
    {
        return Policy<T>
            .Handle<Exception>(ex => ErrorClassifier.IsTransient(ex))
            .WaitAndRetryAsync(
                retryCount: maxRetryAttempts,
                sleepDurationProvider: attempt => CalculateExponentialBackoffWithJitter(attempt, baseDelayMs),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var exception = outcome.Exception;
                    logger?.LogWarning(
                        exception,
                        "Retry {RetryCount}/{MaxRetries} for {Operation} after {Delay}ms. Error: {ErrorMessage}",
                        retryCount,
                        maxRetryAttempts,
                        operationName,
                        timespan.TotalMilliseconds,
                        exception?.Message);
                });
    }

    /// <summary>
    /// Creates an async retry policy for void operations (no return value).
    /// </summary>
    /// <param name="maxRetryAttempts">Maximum number of retry attempts (default: 3)</param>
    /// <param name="baseDelayMs">Base delay in milliseconds for exponential backoff (default: 1000ms)</param>
    /// <param name="logger">Logger for recording retry attempts</param>
    /// <param name="operationName">Name of the operation being retried (for logging)</param>
    /// <returns>A configured async retry policy for void operations</returns>
    public static AsyncRetryPolicy CreateRetryPolicyForVoid(
        int maxRetryAttempts = 3,
        int baseDelayMs = 1000,
        ILogger? logger = null,
        string operationName = "operation")
    {
        return Policy
            .Handle<Exception>(ex => ErrorClassifier.IsTransient(ex))
            .WaitAndRetryAsync(
                retryCount: maxRetryAttempts,
                sleepDurationProvider: attempt => CalculateExponentialBackoffWithJitter(attempt, baseDelayMs),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    logger?.LogWarning(
                        exception,
                        "Retry {RetryCount}/{MaxRetries} for {Operation} after {Delay}ms. Error: {ErrorMessage}",
                        retryCount,
                        maxRetryAttempts,
                        operationName,
                        timespan.TotalMilliseconds,
                        exception.Message);
                });
    }

    /// <summary>
    /// Calculates exponential backoff delay with jitter to prevent thundering herd.
    /// Formula: baseDelay * 2^(attempt-1) * (1 + random jitter between 0 and 0.5)
    /// </summary>
    /// <param name="attempt">The retry attempt number (1-based)</param>
    /// <param name="baseDelayMs">Base delay in milliseconds</param>
    /// <returns>Calculated delay with jitter</returns>
    private static TimeSpan CalculateExponentialBackoffWithJitter(int attempt, int baseDelayMs)
    {
        // Exponential backoff: baseDelay * 2^(attempt-1)
        var exponentialDelay = baseDelayMs * Math.Pow(2, attempt - 1);

        // Add jitter (up to 50% of delay) to prevent thundering herd
        var jitter = Random.Shared.NextDouble() * 0.5;
        var totalDelayMs = exponentialDelay * (1 + jitter);

        return TimeSpan.FromMilliseconds(totalDelayMs);
    }

    /// <summary>
    /// Executes an async operation with retry policy.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="operation">The async operation to execute</param>
    /// <param name="maxRetryAttempts">Maximum number of retry attempts</param>
    /// <param name="baseDelayMs">Base delay in milliseconds for exponential backoff</param>
    /// <param name="logger">Logger for recording retry attempts</param>
    /// <param name="operationName">Name of the operation being retried (for logging)</param>
    /// <returns>The result of the operation</returns>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetryAttempts = 3,
        int baseDelayMs = 1000,
        ILogger? logger = null,
        string operationName = "operation")
    {
        var policy = CreateRetryPolicy<T>(maxRetryAttempts, baseDelayMs, logger, operationName);
        return await policy.ExecuteAsync(operation);
    }

    /// <summary>
    /// Executes an async void operation with retry policy.
    /// </summary>
    /// <param name="operation">The async operation to execute</param>
    /// <param name="maxRetryAttempts">Maximum number of retry attempts</param>
    /// <param name="baseDelayMs">Base delay in milliseconds for exponential backoff</param>
    /// <param name="logger">Logger for recording retry attempts</param>
    /// <param name="operationName">Name of the operation being retried (for logging)</param>
    public static async Task ExecuteWithRetryAsync(
        Func<Task> operation,
        int maxRetryAttempts = 3,
        int baseDelayMs = 1000,
        ILogger? logger = null,
        string operationName = "operation")
    {
        var policy = CreateRetryPolicyForVoid(maxRetryAttempts, baseDelayMs, logger, operationName);
        await policy.ExecuteAsync(operation);
    }
}

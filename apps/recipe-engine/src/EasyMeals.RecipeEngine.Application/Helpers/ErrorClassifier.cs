using System.Net.Sockets;
using System.Text.Json;

namespace EasyMeals.RecipeEngine.Application.Helpers;

/// <summary>
///     Helper class for classifying exceptions as transient or permanent errors.
///     This determines whether an operation should be retried or skipped.
///     Transient Errors (Should Retry):
///     - Network issues: HttpRequestException, SocketException
///     - Timeouts: TaskCanceledException, TimeoutException
///     - I/O errors: IOException
///     Permanent Errors (Should Skip):
///     - Data validation: JsonException, FormatException
///     - Logic errors: NullReferenceException, InvalidOperationException, ArgumentException
/// </summary>
public static class ErrorClassifier
{
    /// <summary>
    ///     Determines if an exception represents a transient error that can be retried.
    /// </summary>
    /// <param name="exception">The exception to classify</param>
    /// <returns>True if the error is transient and should be retried, false if permanent</returns>
    public static bool IsTransient(Exception exception)
	{
		return exception switch
		{
			HttpRequestException => true,
			TaskCanceledException => true,
			SocketException => true,
			IOException => true,
			TimeoutException => true,
			_ => false
		};
	}

    /// <summary>
    ///     Determines if an exception represents a permanent error that should not be retried.
    /// </summary>
    /// <param name="exception">The exception to classify</param>
    /// <returns>True if the error is permanent and should be skipped, false if transient</returns>
    public static bool IsPermanent(Exception exception)
	{
		return exception switch
		{
			JsonException => true,
			ArgumentNullException => true,
			ArgumentException => true,
			NullReferenceException => true,
			InvalidOperationException => true,
			FormatException => true,
			_ => !IsTransient(exception) // If not transient and not explicitly permanent, treat as permanent
		};
	}

    /// <summary>
    ///     Gets a descriptive error type for logging and diagnostics.
    /// </summary>
    /// <param name="exception">The exception to describe</param>
    /// <returns>A descriptive error type string</returns>
    public static string GetErrorType(Exception exception)
	{
		return exception switch
		{
			HttpRequestException => "Network",
			TaskCanceledException => "Timeout",
			SocketException => "Network",
			IOException => "IO",
			TimeoutException => "Timeout",
			JsonException => "DataValidation",
			ArgumentException => "InvalidInput",
			NullReferenceException => "MissingData",
			InvalidOperationException => "LogicError",
			FormatException => "DataFormat",
			_ => "Unknown"
		};
	}
}
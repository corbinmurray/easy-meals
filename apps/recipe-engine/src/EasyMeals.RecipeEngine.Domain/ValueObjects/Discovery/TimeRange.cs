namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;

/// <summary>
///     Value object representing a time range for statistics and queries
/// </summary>
public sealed record TimeRange
{
	public TimeRange(DateTime startTime, DateTime endTime)
	{
		if (startTime >= endTime)
			throw new ArgumentException("Start time must be before end time");

		StartTime = startTime;
		EndTime = endTime;
	}

	/// <summary>Start of the time range</summary>
	public DateTime StartTime { get; init; }

	/// <summary>End of the time range</summary>
	public DateTime EndTime { get; init; }

	/// <summary>
	///     Duration of the time range
	/// </summary>
	public TimeSpan Duration => EndTime - StartTime;

	/// <summary>
	///     Creates a time range for the last N hours
	/// </summary>
	public static TimeRange LastHours(int hours)
	{
		DateTime end = DateTime.UtcNow;
		DateTime start = end.AddHours(-hours);
		return new TimeRange(start, end);
	}

	/// <summary>
	///     Creates a time range for the last N days
	/// </summary>
	public static TimeRange LastDays(int days)
	{
		DateTime end = DateTime.UtcNow;
		DateTime start = end.AddDays(-days);
		return new TimeRange(start, end);
	}

	/// <summary>
	///     Creates a time range for today
	/// </summary>
	public static TimeRange Today => new(DateTime.Today, DateTime.Today.AddDays(1));

	/// <summary>
	///     Creates a time range for this week
	/// </summary>
	public static TimeRange ThisWeek
	{
		get
		{
			DateTime today = DateTime.Today;
			DateTime startOfWeek = today.AddDays(-(int)today.DayOfWeek);
			return new TimeRange(startOfWeek, startOfWeek.AddDays(7));
		}
	}
}
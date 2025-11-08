using EasyMeals.RecipeEngine.Domain.Entities;
using EasyMeals.RecipeEngine.Domain.Services;

namespace EasyMeals.RecipeEngine.Infrastructure.Services;

/// <summary>
///     Policy for determining when a batch should stop processing.
/// </summary>
public class BatchCompletionPolicy : IBatchCompletionPolicy
{
	public bool ShouldCompleteBatch(RecipeBatch batch, DateTime currentTime)
	{
		if (batch == null) throw new ArgumentNullException(nameof(batch));

		BatchCompletionReason reason = GetCompletionReason(batch, currentTime);
		return reason != BatchCompletionReason.NotComplete;
	}

	public BatchCompletionReason GetCompletionReason(RecipeBatch batch, DateTime currentTime)
	{
		if (batch == null) throw new ArgumentNullException(nameof(batch));

		bool sizeReached = batch.ProcessedCount >= batch.BatchSize;
		bool timeExceeded = currentTime - batch.StartedAt >= batch.TimeWindow;

		if (sizeReached && timeExceeded) return BatchCompletionReason.Both;
		if (sizeReached) return BatchCompletionReason.BatchSizeReached;
		if (timeExceeded) return BatchCompletionReason.TimeWindowExceeded;

		return BatchCompletionReason.NotComplete;
	}
}
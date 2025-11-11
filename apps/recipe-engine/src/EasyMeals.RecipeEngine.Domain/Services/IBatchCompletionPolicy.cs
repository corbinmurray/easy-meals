using EasyMeals.RecipeEngine.Domain.Entities;

namespace EasyMeals.RecipeEngine.Domain.Services;

/// <summary>
///     Domain service for determining batch completion based on business rules.
/// </summary>
public interface IBatchCompletionPolicy
{
    /// <summary>
    ///     Determine if a batch should complete based on size and time window.
    /// </summary>
    bool ShouldCompleteBatch(RecipeBatch batch, DateTime currentTime);

    /// <summary>
    ///     Get the reason why a batch should complete.
    /// </summary>
    BatchCompletionReason GetCompletionReason(RecipeBatch batch, DateTime currentTime);
}

public enum BatchCompletionReason
{
    NotComplete,
    BatchSizeReached,
    TimeWindowExceeded,
    Both
}
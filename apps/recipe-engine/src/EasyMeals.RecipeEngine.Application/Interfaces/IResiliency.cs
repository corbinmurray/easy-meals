namespace EasyMeals.RecipeEngine.Application.Interfaces;

/// <summary>
///     Resilience patterns contract for handling transient failures.
/// </summary>
public interface IResiliency
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, ResiliencyPolicy policy, CancellationToken cancellationToken = default);
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
    Task ExecuteAsync(Func<CancellationToken, Task> operation, ResiliencyPolicy policy, CancellationToken cancellationToken = default);
}

public record ResiliencyPolicy(
    int RetryCount = 3,
    TimeSpan BaseDelay = default,
    TimeSpan CircuitBreakerTimeout = default,
    bool UseExponentialBackoff = true);

public static class ResiliencyPolicies
{
    public static readonly ResiliencyPolicy Default = new();
    public static readonly ResiliencyPolicy Aggressive = new(5, TimeSpan.FromMilliseconds(100));
    public static readonly ResiliencyPolicy Conservative = new(2, TimeSpan.FromSeconds(1));
}
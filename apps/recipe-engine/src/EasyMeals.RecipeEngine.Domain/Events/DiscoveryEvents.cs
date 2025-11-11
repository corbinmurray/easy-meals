using EasyMeals.RecipeEngine.Domain.ValueObjects.Discovery;

namespace EasyMeals.RecipeEngine.Domain.Events;

/// <summary>
///     Event raised when recipe URLs are discovered
/// </summary>
/// <param name="DiscoveryId">Unique ID for this discovery session</param>
/// <param name="DiscoveredUrls">Collection of discovered URLs</param>
/// <param name="Provider">Source provider name</param>
/// <param name="DiscoveryOptions">Options used for discovery</param>
/// <param name="TotalDiscovered">Total number of URLs discovered</param>
public sealed record RecipeUrlsDiscoveredEvent(
    Guid DiscoveryId,
    IEnumerable<DiscoveredUrl> DiscoveredUrls,
    string Provider,
    DiscoveryOptions DiscoveryOptions,
    int TotalDiscovered) : BaseDomainEvent;

/// <summary>
///     Event raised when discovery process starts
/// </summary>
/// <param name="DiscoveryId">Unique ID for this discovery session</param>
/// <param name="BaseUrl">Base URL being discovered</param>
/// <param name="Provider">Source provider name</param>
/// <param name="DiscoveryOptions">Discovery configuration</param>
public sealed record DiscoveryStartedEvent(
    Guid DiscoveryId,
    string BaseUrl,
    string Provider,
    DiscoveryOptions DiscoveryOptions) : BaseDomainEvent;

/// <summary>
///     Event raised when discovery process completes
/// </summary>
/// <param name="DiscoveryId">Unique ID for this discovery session</param>
/// <param name="Provider">Source provider name</param>
/// <param name="TotalUrlsDiscovered">Total URLs discovered</param>
/// <param name="RecipeUrlsFound">Number of recipe URLs found</param>
/// <param name="Duration">How long discovery took</param>
/// <param name="Statistics">Discovery statistics</param>
public sealed record DiscoveryCompletedEvent(
    Guid DiscoveryId,
    string Provider,
    int TotalUrlsDiscovered,
    int RecipeUrlsFound,
    TimeSpan Duration,
    DiscoveryStatistics Statistics) : BaseDomainEvent;

/// <summary>
///     Event raised when discovery fails
/// </summary>
/// <param name="DiscoveryId">Unique ID for this discovery session</param>
/// <param name="BaseUrl">Base URL that failed discovery</param>
/// <param name="Provider">Source provider name</param>
/// <param name="ErrorMessage">Error message</param>
/// <param name="Exception">Exception details</param>
public sealed record DiscoveryFailedEvent(
    Guid DiscoveryId,
    string BaseUrl,
    string Provider,
    string ErrorMessage,
    string? Exception) : BaseDomainEvent;

/// <summary>
///     Event raised when a high-confidence recipe URL is found
/// </summary>
/// <param name="DiscoveryId">Discovery session ID</param>
/// <param name="DiscoveredUrl">The high-confidence recipe URL</param>
public sealed record HighConfidenceRecipeFoundEvent(
    Guid DiscoveryId,
    DiscoveredUrl DiscoveredUrl) : BaseDomainEvent;
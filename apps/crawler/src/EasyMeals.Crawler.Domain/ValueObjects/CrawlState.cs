namespace EasyMeals.Crawler.Domain.ValueObjects;

/// <summary>
///     Represents the current state of a crawling session
/// </summary>
public record CrawlState
{
    public IEnumerable<string> PendingUrls { get; init; } = [];
    public HashSet<string> CompletedRecipeIds { get; init; } = [];
    public HashSet<string> FailedUrls { get; init; } = [];
    public DateTime LastCrawlTime { get; init; } = DateTime.MinValue;
    public int TotalProcessed { get; init; }
    public int TotalSuccessful { get; init; }
    public int TotalFailed { get; init; }

    /// <summary>
    ///     Marks a recipe as successfully processed
    /// </summary>
    public CrawlState MarkAsCompleted(string recipeId, string url)
    {
        return this with
        {
            CompletedRecipeIds = CompletedRecipeIds.Concat([recipeId]).ToHashSet(),
            PendingUrls = PendingUrls.Where(u => u != url).ToList(),
            TotalProcessed = TotalProcessed + 1,
            TotalSuccessful = TotalSuccessful + 1,
            LastCrawlTime = DateTime.UtcNow
        };
    }

    /// <summary>
    ///     Marks a URL as failed
    /// </summary>
    public CrawlState MarkAsFailed(string url)
    {
        return this with
        {
            FailedUrls = FailedUrls.Concat([url]).ToHashSet(),
            PendingUrls = PendingUrls.Where(u => u != url).ToList(),
            TotalProcessed = TotalProcessed + 1,
            TotalFailed = TotalFailed + 1,
            LastCrawlTime = DateTime.UtcNow
        };
    }
}
namespace EasyMeals.Crawler.Domain.ValueObjects;

/// <summary>
/// Represents the current state of a crawling session
/// </summary>
public record CrawlState
{
    public List<string> PendingUrls { get; init; } = new();
    public HashSet<string> CompletedRecipeIds { get; init; } = new();
    public HashSet<string> FailedUrls { get; init; } = new();
    public DateTime LastCrawlTime { get; init; } = DateTime.MinValue;
    public int TotalProcessed { get; init; } = 0;
    public int TotalSuccessful { get; init; } = 0;
    public int TotalFailed { get; init; } = 0;

    /// <summary>
    /// Creates a new crawl state with the provided pending URLs
    /// </summary>
    public static CrawlState Create(IEnumerable<string> pendingUrls)
    {
        return new CrawlState
        {
            PendingUrls = pendingUrls.ToList(),
            LastCrawlTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Marks a recipe as successfully processed
    /// </summary>
    public CrawlState MarkAsCompleted(string recipeId, string url)
    {
        return this with
        {
            CompletedRecipeIds = CompletedRecipeIds.Concat(new[] { recipeId }).ToHashSet(),
            PendingUrls = PendingUrls.Where(u => u != url).ToList(),
            TotalProcessed = TotalProcessed + 1,
            TotalSuccessful = TotalSuccessful + 1,
            LastCrawlTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Marks a URL as failed
    /// </summary>
    public CrawlState MarkAsFailed(string url)
    {
        return this with
        {
            FailedUrls = FailedUrls.Concat(new[] { url }).ToHashSet(),
            PendingUrls = PendingUrls.Where(u => u != url).ToList(),
            TotalProcessed = TotalProcessed + 1,
            TotalFailed = TotalFailed + 1,
            LastCrawlTime = DateTime.UtcNow
        };
    }
}

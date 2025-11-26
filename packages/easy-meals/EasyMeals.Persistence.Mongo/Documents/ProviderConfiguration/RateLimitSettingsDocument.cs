using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.Persistence.Mongo.Documents.ProviderConfiguration;

/// <summary>
/// Embedded document for rate limit settings.
/// </summary>
public class RateLimitSettingsDocument
{
    [BsonElement("requestsPerMinute")]
    public int RequestsPerMinute { get; set; } = 60;

    [BsonElement("delayBetweenRequestsMs")]
    public int DelayBetweenRequestsMs { get; set; } = 100;

    [BsonElement("maxConcurrentRequests")]
    public int MaxConcurrentRequests { get; set; } = 5;

    [BsonElement("maxRetries")]
    public int MaxRetries { get; set; } = 3;

    [BsonElement("retryDelayMs")]
    public int RetryDelayMs { get; set; } = 1000;
}

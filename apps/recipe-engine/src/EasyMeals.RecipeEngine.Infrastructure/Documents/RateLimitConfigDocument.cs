using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.RecipeEngine.Infrastructure.Documents;

/// <summary>
///     Embedded document for rate limit settings per provider.
///     Maps to Domain.ValueObjects.RateLimitConfig
/// </summary>
public class RateLimitConfigDocument
{
    [BsonElement("minDelaySeconds")]
    [BsonRequired]
    public double MinDelaySeconds { get; set; }

    [BsonElement("maxRequestsPerMinute")]
    [BsonRequired]
    public int MaxRequestsPerMinute { get; set; }

    [BsonElement("retryCount")]
    [BsonRequired]
    public int RetryCount { get; set; }

    [BsonElement("requestTimeoutSeconds")]
    [BsonRequired]
    public int RequestTimeoutSeconds { get; set; }
}

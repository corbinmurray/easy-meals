using EasyMeals.Shared.Data.Attributes;
using EasyMeals.Shared.Data.Documents;
using EasyMeals.RecipeEngine.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.RecipeEngine.Infrastructure.Documents;

/// <summary>
/// MongoDB document for provider-specific configuration settings.
/// Stored in database to keep sensitive URLs and rate limits private.
/// </summary>
[BsonCollection("provider_configurations")]
public class ProviderConfigurationDocument : BaseDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("providerId")]
    [BsonRequired]
    public string ProviderId { get; set; } = string.Empty;

    [BsonElement("enabled")]
    [BsonRequired]
    public bool Enabled { get; set; }

    [BsonElement("discoveryStrategy")]
    [BsonRequired]
    public string DiscoveryStrategy { get; set; } = string.Empty;

    [BsonElement("recipeRootUrl")]
    [BsonRequired]
    public string RecipeRootUrl { get; set; } = string.Empty;

    [BsonElement("batchSize")]
    [BsonRequired]
    public int BatchSize { get; set; }

    [BsonElement("timeWindowMinutes")]
    [BsonRequired]
    public int TimeWindowMinutes { get; set; }

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

    [BsonElement("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    [BsonElement("updatedBy")]
    public string? UpdatedBy { get; set; }
}
